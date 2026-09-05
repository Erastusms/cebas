using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CEBAS.Api.Features.Safety.Reports.CreateReport;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Services;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.UnitTests;

public class CreateReportSliceUnitTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Validator_ShouldFail_WhenCategoryIsInvalid()
    {
        var validator = new CreateReportCommandValidator();
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "INVALID_CATEGORY",
            "Spammy post"
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Category");
    }

    [Fact]
    public void Validator_ShouldFail_WhenBothTargetsAreNull()
    {
        var validator = new CreateReportCommandValidator();
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            null,
            null,
            "SPAM",
            "No target"
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("A report must target either a post or a user"));
    }

    [Fact]
    public void Validator_ShouldFail_WhenBothTargetsAreProvided()
    {
        var validator = new CreateReportCommandValidator();
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SPAM",
            "Both targets"
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("A report must target either a post or a user"));
    }

    [Fact]
    public void Validator_ShouldSucceed_WhenTargetIsOnlyPost()
    {
        var validator = new CreateReportCommandValidator();
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "SPAM",
            "Valid post report"
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldSucceed_WhenTargetIsOnlyUser()
    {
        var validator = new CreateReportCommandValidator();
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            "HARASSMENT",
            "Valid user report"
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenReporterDoesNotExist()
    {
        using var dbContext = CreateDbContext();
        var outbox = new OutboxWriter(dbContext);
        var handler = new CreateReportCommandHandler(dbContext, outbox, NullLogger<CreateReportCommandHandler>.Instance);

        var command = new CreateReportCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "SPAM",
            "Missing reporter"
        );

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenReporterIsSuspended()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("baduser", "bad@test.com", "hash", "Bad User");
        reporter.Suspend("Spam behavior");
        await dbContext.Users.AddAsync(reporter);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new CreateReportCommandHandler(dbContext, outbox, NullLogger<CreateReportCommandHandler>.Instance);

        var command = new CreateReportCommand(
            reporter.Id,
            Guid.NewGuid(),
            null,
            "SPAM",
            "Report from suspended user"
        );

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*suspended*");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTargetPostDoesNotExist()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        await dbContext.Users.AddAsync(reporter);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new CreateReportCommandHandler(dbContext, outbox, NullLogger<CreateReportCommandHandler>.Instance);

        var command = new CreateReportCommand(
            reporter.Id,
            Guid.NewGuid(),
            null,
            "SPAM",
            "Missing post"
        );

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidation_WhenReporterReportsOwnPost()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var post = Post.Create(reporter.Id, "My own post", 0);
        await dbContext.Users.AddAsync(reporter);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new CreateReportCommandHandler(dbContext, outbox, NullLogger<CreateReportCommandHandler>.Instance);

        var command = new CreateReportCommand(
            reporter.Id,
            post.Id,
            null,
            "SPAM",
            "Reporting own post"
        );

        var act = () => handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*cannot report your own post*");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidation_WhenReporterReportsSelf()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        await dbContext.Users.AddAsync(reporter);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new CreateReportCommandHandler(dbContext, outbox, NullLogger<CreateReportCommandHandler>.Instance);

        var command = new CreateReportCommand(
            reporter.Id,
            null,
            reporter.Id,
            "HARASSMENT",
            "Reporting self"
        );

        var act = () => handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*cannot report your own account*");
    }

    [Fact]
    public async Task Handle_ShouldThrowConflict_WhenDuplicatePendingReportExists()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var targetUser = User.Create("spammer", "spammer@test.com", "hash", "Spammer");
        var post = Post.Create(targetUser.Id, "Spam post", 0);

        var existingReport = Report.CreateForPost(reporter.Id, post.Id, ReportCategory.SPAM, "First report");

        await dbContext.Users.AddRangeAsync(reporter, targetUser);
        await dbContext.Posts.AddAsync(post);
        await dbContext.Reports.AddAsync(existingReport);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new CreateReportCommandHandler(dbContext, outbox, NullLogger<CreateReportCommandHandler>.Instance);

        var command = new CreateReportCommand(
            reporter.Id,
            post.Id,
            null,
            "SPAM",
            "Second report"
        );

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*pending report*");
    }

    [Fact]
    public async Task Handle_ShouldSucceed_AndEnqueueOutboxEvent_WhenReportingPost()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var targetUser = User.Create("author", "author@test.com", "hash", "Author");
        var post = Post.Create(targetUser.Id, "Offensive post", 0);

        await dbContext.Users.AddRangeAsync(reporter, targetUser);
        await dbContext.Posts.AddAsync(post);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new CreateReportCommandHandler(dbContext, outbox, NullLogger<CreateReportCommandHandler>.Instance);

        var command = new CreateReportCommand(
            reporter.Id,
            post.Id,
            null,
            "HATE_SPEECH",
            "Contains hate speech"
        );

        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.TargetPostId.Should().Be(post.Id);
        response.TargetUserId.Should().BeNull();
        response.Category.Should().Be("HATE_SPEECH");
        response.Status.Should().Be("PENDING");

        var dbReport = await dbContext.Reports.FirstOrDefaultAsync(r => r.Id == response.Id);
        dbReport.Should().NotBeNull();
        dbReport!.ReporterUserId.Should().Be(reporter.Id);

        var outboxEvent = await dbContext.OutboxEvents.FirstOrDefaultAsync(e => e.AggregateId == response.Id);
        outboxEvent.Should().NotBeNull();
        outboxEvent!.EventType.Should().Be("ReportCreated");
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenReportingUser()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var targetUser = User.Create("troll", "troll@test.com", "hash", "Troll");

        await dbContext.Users.AddRangeAsync(reporter, targetUser);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new CreateReportCommandHandler(dbContext, outbox, NullLogger<CreateReportCommandHandler>.Instance);

        var command = new CreateReportCommand(
            reporter.Id,
            null,
            targetUser.Id,
            "HARASSMENT",
            "Harassing in DMs and mentions"
        );

        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.TargetUserId.Should().Be(targetUser.Id);
        response.TargetPostId.Should().BeNull();
        response.Category.Should().Be("HARASSMENT");
        response.Status.Should().Be("PENDING");

        var dbReport = await dbContext.Reports.FirstOrDefaultAsync(r => r.Id == response.Id);
        dbReport.Should().NotBeNull();
        dbReport!.TargetUserId.Should().Be(targetUser.Id);
    }
}
