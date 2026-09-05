using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;
using CEBAS.Api.Features.Notifications.GetNotifications;
using CEBAS.Api.Features.Notifications.GetUnreadCount;
using CEBAS.Api.Features.Notifications.MarkAllNotificationsRead;
using CEBAS.Api.Features.Notifications.MarkNotificationRead;
using CEBAS.Application.Contracts.Events;
using CEBAS.Domain.Common;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Services;

namespace CEBAS.UnitTests;

public class OutboxAndNotificationUnitTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Notification_Create_ValidParameters_ShouldInstantiateWithUnreadState()
    {
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var notification = Notification.Create(
            recipientId,
            actorId,
            NotificationType.PostLiked,
            targetId: Guid.NewGuid(),
            targetType: "POST"
        );

        notification.Should().NotBeNull();
        notification.RecipientId.Should().Be(recipientId);
        notification.ActorId.Should().Be(actorId);
        notification.Type.Should().Be(NotificationType.PostLiked);
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Notification_Create_WhenSelfNotification_ShouldThrowValidationException()
    {
        var userId = Guid.NewGuid();

        var act = () => Notification.Create(userId, userId, NotificationType.PostLiked);

        act.Should().Throw<ValidationException>()
            .Which.Errors["Notification"].Should().Contain("A user cannot receive notifications from themselves.");
    }

    [Fact]
    public void Notification_MarkAsRead_WhenUnread_ShouldMarkReadAndSetReadAt()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.UserFollowed
        );

        var now = DateTimeOffset.UtcNow;
        notification.MarkAsRead(now);

        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().Be(now);

        // Idempotent second call should not overwrite ReadAt
        var later = now.AddMinutes(5);
        notification.MarkAsRead(later);
        notification.ReadAt.Should().Be(now);
    }

    [Fact]
    public void OutboxEvent_Create_ValidParameters_ShouldInitializeWithPendingStatus()
    {
        var aggregateId = Guid.NewGuid();
        var evt = OutboxEvent.Create(
            eventType: "POST_CREATED",
            aggregateType: "Post",
            aggregateId: aggregateId,
            payload: "{\"test\": 123}",
            maxRetries: 3
        );

        evt.Should().NotBeNull();
        evt.EventType.Should().Be("POST_CREATED");
        evt.AggregateType.Should().Be("Post");
        evt.AggregateId.Should().Be(aggregateId);
        evt.Status.Should().Be(OutboxEventStatus.Pending);
        evt.AttemptCount.Should().Be(0);
        evt.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void OutboxEvent_StateTransitions_Processing_Published_FailedWithBackoff()
    {
        var evt = OutboxEvent.Create("POST_LIKED", "Post", Guid.NewGuid(), "{}", maxRetries: 2);
        var now = DateTimeOffset.UtcNow;

        // 1. Mark Processing
        evt.MarkProcessing(now);
        evt.Status.Should().Be(OutboxEventStatus.Processing);
        evt.AttemptCount.Should().Be(1);

        // 2. Transient failure with backoff -> should return to Pending with future NextAttemptAt
        var backoff = TimeSpan.FromSeconds(5);
        evt.MarkFailed("Network timeout", backoff, now);
        evt.Status.Should().Be(OutboxEventStatus.Pending);
        evt.NextAttemptAt.Should().Be(now.Add(backoff));

        // 3. Mark Processing again (Attempt 2)
        evt.MarkProcessing(now.Add(backoff));
        evt.AttemptCount.Should().Be(2);

        // 4. Second failure exceeds MaxRetries (2) -> should become Failed
        evt.MarkFailed("Hard error", backoff, now);
        evt.Status.Should().Be(OutboxEventStatus.Failed);
    }

    [Fact]
    public async Task OutboxWriter_EnqueueAsync_ShouldStageOutboxEventInChangeTrackerWithoutSaving()
    {
        using var dbContext = CreateInMemoryDbContext();
        var writer = new OutboxWriter(dbContext);

        var payload = new PostLikedPayload(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await writer.EnqueueAsync(
            eventType: "POST_LIKED",
            aggregateType: "Post",
            aggregateId: payload.PostId,
            payload: payload
        );

        // Should be in ChangeTracker as Added, but not yet committed to DB
        dbContext.ChangeTracker.Entries<OutboxEvent>().Should().HaveCount(1);
        var staged = dbContext.ChangeTracker.Entries<OutboxEvent>().First().Entity;
        staged.EventType.Should().Be("POST_LIKED");
        staged.AggregateId.Should().Be(payload.PostId);
        staged.Status.Should().Be(OutboxEventStatus.Pending);

        // Commit explicitly
        await dbContext.SaveChangesAsync();
        (await dbContext.OutboxEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task MarkNotificationReadCommandHandler_WhenOwnNotification_ShouldMarkRead()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var notification = Notification.Create(userId, actorId, NotificationType.PostLiked, Guid.NewGuid(), "POST");
        await dbContext.Notifications.AddAsync(notification);
        await dbContext.SaveChangesAsync();

        var handler = new MarkNotificationReadCommandHandler(dbContext, NullLogger<MarkNotificationReadCommandHandler>.Instance);
        var response = await handler.Handle(new MarkNotificationReadCommand(userId, notification.Id), CancellationToken.None);

        response.Id.Should().Be(notification.Id);
        response.IsRead.Should().BeTrue();
        response.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkNotificationReadCommandHandler_WhenOtherUsersNotification_ShouldThrowForbidden()
    {
        using var dbContext = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var notification = Notification.Create(ownerId, actorId, NotificationType.PostLiked, Guid.NewGuid(), "POST");
        await dbContext.Notifications.AddAsync(notification);
        await dbContext.SaveChangesAsync();

        var handler = new MarkNotificationReadCommandHandler(dbContext, NullLogger<MarkNotificationReadCommandHandler>.Instance);
        var act = async () => await handler.Handle(new MarkNotificationReadCommand(attackerId, notification.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task MarkAllNotificationsReadCommandHandler_ShouldMarkAllUnreadAsRead()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var n1 = Notification.Create(userId, actorId, NotificationType.PostLiked);
        var n2 = Notification.Create(userId, actorId, NotificationType.UserFollowed);
        var nOther = Notification.Create(Guid.NewGuid(), actorId, NotificationType.PostLiked);

        await dbContext.Notifications.AddRangeAsync(n1, n2, nOther);
        await dbContext.SaveChangesAsync();

        var handler = new MarkAllNotificationsReadCommandHandler(dbContext, NullLogger<MarkAllNotificationsReadCommandHandler>.Instance);
        var response = await handler.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        response.MarkedReadCount.Should().Be(2);

        var unreadCount = await dbContext.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);
        unreadCount.Should().Be(0);

        // Other user's notification remains unread
        (await dbContext.Notifications.FirstAsync(n => n.Id == nOther.Id)).IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetUnreadCountQueryHandler_ShouldReturnCorrectCount()
    {
        using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var n1 = Notification.Create(userId, actorId, NotificationType.PostLiked);
        var n2 = Notification.Create(userId, actorId, NotificationType.UserFollowed);
        n2.MarkAsRead(DateTimeOffset.UtcNow);

        await dbContext.Notifications.AddRangeAsync(n1, n2);
        await dbContext.SaveChangesAsync();

        var handler = new GetUnreadCountQueryHandler(dbContext, NullLogger<GetUnreadCountQueryHandler>.Instance);
        var result = await handler.Handle(new GetUnreadCountQuery(userId), CancellationToken.None);

        result.UnreadCount.Should().Be(1);
    }
}
