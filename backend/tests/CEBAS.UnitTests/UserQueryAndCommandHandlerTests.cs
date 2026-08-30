using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CEBAS.Api.Features.Users.GetCurrentUser;
using CEBAS.Api.Features.Users.GetPublicProfile;
using CEBAS.Api.Features.Users.GetSessions;
using CEBAS.Api.Features.Users.RevokeSession;
using CEBAS.Api.Features.Users.UpdateProfile;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.UnitTests;

public class UserQueryAndCommandHandlerTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetPublicProfile_ExistingUser_ShouldReturnProfileWithStats()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("johndoe", "john@example.com", "hash", "John Doe", "My Bio");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var handler = new GetPublicProfileQueryHandler(dbContext);
        var result = await handler.Handle(new GetPublicProfileQuery("JohnDoe"), CancellationToken.None);

        result.Should().NotBeNull();
        result.Username.Should().Be("johndoe");
        result.DisplayName.Should().Be("John Doe");
        result.Bio.Should().Be("My Bio");
        result.Stats.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPublicProfile_NonExistentUser_ShouldThrowNotFoundException()
    {
        using var dbContext = CreateDbContext();
        var handler = new GetPublicProfileQueryHandler(dbContext);

        var act = () => handler.Handle(new GetPublicProfileQuery("ghost"), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*was not found*");
    }

    [Fact]
    public async Task GetCurrentUser_ExistingUser_ShouldReturnCurrentUserResponse()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("alice", "alice@example.com", "hash", "Alice W");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserQueryHandler(dbContext);
        var result = await handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.Username.Should().Be("alice");
    }

    [Fact]
    public async Task UpdateProfile_ValidData_ShouldUpdateUserAndPersist()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("bob", "bob@example.com", "hash", "Bob Initial");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(dbContext, NullLogger<UpdateProfileCommandHandler>.Instance);
        var command = new UpdateProfileCommand(user.Id, "Bob Updated", "New Bio");

        var result = await handler.Handle(command, CancellationToken.None);

        result.DisplayName.Should().Be("Bob Updated");
        result.Bio.Should().Be("New Bio");

        var persisted = await dbContext.Users.FindAsync(user.Id);
        persisted!.DisplayName.Should().Be("Bob Updated");
        persisted.Bio.Should().Be("New Bio");
    }

    [Fact]
    public async Task GetSessions_ShouldReturnActiveSessions_AndMarkCurrentSession()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("carol", "carol@example.com", "hash", "Carol");
        var active1 = Session.Create(user.Id, "hash1", DateTimeOffset.UtcNow.AddDays(5), "Desktop", "1.1.1.1");
        var active2 = Session.Create(user.Id, "hash2", DateTimeOffset.UtcNow.AddDays(5), "Mobile", "2.2.2.2");
        var revoked = Session.Create(user.Id, "hash3", DateTimeOffset.UtcNow.AddDays(5), "Old Device", "3.3.3.3");
        revoked.Revoke(DateTimeOffset.UtcNow);

        await dbContext.Users.AddAsync(user);
        await dbContext.Sessions.AddRangeAsync(active1, active2, revoked);
        await dbContext.SaveChangesAsync();

        var handler = new GetSessionsQueryHandler(dbContext);
        var result = await handler.Handle(new GetSessionsQuery(user.Id, active1.Id), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Id == active1.Id && s.IsCurrent);
        result.Should().Contain(s => s.Id == active2.Id && !s.IsCurrent);
        result.Should().NotContain(s => s.Id == revoked.Id);
    }

    [Fact]
    public async Task RevokeSession_OwnSession_ShouldRevokeSuccessfully()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("dave", "dave@example.com", "hash", "Dave");
        var session = Session.Create(user.Id, "hash_dave", DateTimeOffset.UtcNow.AddDays(1));
        await dbContext.Users.AddAsync(user);
        await dbContext.Sessions.AddAsync(session);
        await dbContext.SaveChangesAsync();

        var handler = new RevokeSessionCommandHandler(dbContext, NullLogger<RevokeSessionCommandHandler>.Instance);
        var result = await handler.Handle(new RevokeSessionCommand(user.Id, session.Id), CancellationToken.None);

        result.Should().BeTrue();
        var updated = await dbContext.Sessions.FindAsync(session.Id);
        updated!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeSession_AnotherUserSession_ShouldThrowForbiddenException()
    {
        using var dbContext = CreateDbContext();
        var user1 = User.Create("user1", "u1@example.com", "hash", "User 1");
        var user2 = User.Create("user2", "u2@example.com", "hash", "User 2");
        var session = Session.Create(user1.Id, "hash1", DateTimeOffset.UtcNow.AddDays(1));

        await dbContext.Users.AddRangeAsync(user1, user2);
        await dbContext.Sessions.AddAsync(session);
        await dbContext.SaveChangesAsync();

        var handler = new RevokeSessionCommandHandler(dbContext, NullLogger<RevokeSessionCommandHandler>.Instance);
        var act = () => handler.Handle(new RevokeSessionCommand(user2.Id, session.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*cannot revoke another user's session*");
    }
}
