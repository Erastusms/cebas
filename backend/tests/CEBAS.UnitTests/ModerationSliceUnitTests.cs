using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CEBAS.Api.Features.Safety.Moderation.ExecuteModerationAction;
using CEBAS.Api.Features.Safety.Moderation.GetReports;
using CEBAS.Api.Features.Safety.Moderation.GetSuspendedUsers;
using CEBAS.Api.Features.Safety.Moderation.UnsuspendUser;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Services;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.UnitTests;

public class ModerationSliceUnitTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Theory]
    [InlineData("RESOLVE")]
    [InlineData("DISMISS")]
    [InlineData("HIDE_POST")]
    [InlineData("SUSPEND_USER")]
    public void Validator_ShouldPass_ForValidActions(string action)
    {
        var validator = new ExecuteModerationActionCommandValidator();
        var command = new ExecuteModerationActionCommand(Guid.NewGuid(), Guid.NewGuid(), action, "Valid reason");

        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ShouldFail_ForInvalidAction()
    {
        var validator = new ExecuteModerationActionCommandValidator();
        var command = new ExecuteModerationActionCommand(Guid.NewGuid(), Guid.NewGuid(), "BAN_FOREVER", "Invalid");

        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Action");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenReportDoesNotExist()
    {
        using var dbContext = CreateDbContext();
        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(Guid.NewGuid(), Guid.NewGuid(), "RESOLVE", "Reason");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldResolveReport_AndCreateAuditLog()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var targetUser = User.Create("target", "target@test.com", "hash", "Target");
        var report = Report.CreateForUser(reporter.Id, targetUser.Id, ReportCategory.HARASSMENT, "Harassment report");

        await dbContext.Users.AddRangeAsync(reporter, targetUser);
        await dbContext.Reports.AddAsync(report);
        await dbContext.SaveChangesAsync();

        var moderatorId = Guid.NewGuid();
        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(report.Id, moderatorId, "RESOLVE", "Resolved after investigation");
        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.Action.Should().Be("RESOLVE");
        response.Status.Should().Be("RESOLVED");

        var dbReport = await dbContext.Reports.FindAsync(report.Id);
        dbReport!.Status.Should().Be(ReportStatus.RESOLVED);
        dbReport.ResolvedByUserId.Should().Be(moderatorId);

        var auditLog = await dbContext.ModerationAuditLogs.FirstOrDefaultAsync(a => a.TargetId == report.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("REPORT_RESOLVED");
        auditLog.ActorUserId.Should().Be(moderatorId);

        var outboxEvent = await dbContext.OutboxEvents.FirstOrDefaultAsync(e => e.EventType == "ReportResolved");
        outboxEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldDismissReport_AndCreateAuditLog()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var targetUser = User.Create("target", "target@test.com", "hash", "Target");
        var report = Report.CreateForUser(reporter.Id, targetUser.Id, ReportCategory.SPAM, "False report");

        await dbContext.Users.AddRangeAsync(reporter, targetUser);
        await dbContext.Reports.AddAsync(report);
        await dbContext.SaveChangesAsync();

        var moderatorId = Guid.NewGuid();
        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(report.Id, moderatorId, "DISMISS", "No violation found");
        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.Action.Should().Be("DISMISS");
        response.Status.Should().Be("DISMISSED");

        var dbReport = await dbContext.Reports.FindAsync(report.Id);
        dbReport!.Status.Should().Be(ReportStatus.DISMISSED);

        var auditLog = await dbContext.ModerationAuditLogs.FirstOrDefaultAsync(a => a.TargetId == report.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("REPORT_DISMISSED");
    }

    [Fact]
    public async Task Handle_HidePost_ShouldMarkPostAsHiddenAndResolveReport()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var author = User.Create("author", "author@test.com", "hash", "Author");
        var post = Post.Create(author.Id, "Inappropriate content", 0);
        var report = Report.CreateForPost(reporter.Id, post.Id, ReportCategory.INAPPROPRIATE_CONTENT, "Bad content");

        await dbContext.Users.AddRangeAsync(reporter, author);
        await dbContext.Posts.AddAsync(post);
        await dbContext.Reports.AddAsync(report);
        await dbContext.SaveChangesAsync();

        var moderatorId = Guid.NewGuid();
        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(report.Id, moderatorId, "HIDE_POST", "Violated community guidelines");
        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.Action.Should().Be("HIDE_POST");
        response.Status.Should().Be("RESOLVED");

        var dbPost = await dbContext.Posts.FindAsync(post.Id);
        dbPost!.IsHidden.Should().BeTrue();
        dbPost.HiddenReason.Should().Be("Violated community guidelines");

        var dbReport = await dbContext.Reports.FindAsync(report.Id);
        dbReport!.Status.Should().Be(ReportStatus.RESOLVED);

        var auditLog = await dbContext.ModerationAuditLogs.FirstOrDefaultAsync(a => a.TargetId == post.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("POST_HIDDEN");

        var outboxEvent = await dbContext.OutboxEvents.FirstOrDefaultAsync(e => e.EventType == "PostHidden");
        outboxEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_HidePost_OnUserReport_ShouldThrowValidationException()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var targetUser = User.Create("target", "target@test.com", "hash", "Target");
        var report = Report.CreateForUser(reporter.Id, targetUser.Id, ReportCategory.HARASSMENT, "Harassment");

        await dbContext.Users.AddRangeAsync(reporter, targetUser);
        await dbContext.Reports.AddAsync(report);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(report.Id, Guid.NewGuid(), "HIDE_POST", "Cannot hide post on user report");
        var act = () => handler.Handle(command, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*only valid for reports targeting a post*");
    }

    [Fact]
    public async Task Handle_SuspendUser_ShouldMarkUserSuspendedAndRevokeSessions()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var spammer = User.Create("spammer", "spammer@test.com", "hash", "Spammer");
        var report = Report.CreateForUser(reporter.Id, spammer.Id, ReportCategory.SPAM, "Spamming everywhere");

        // Create an active session for the spammer
        var session = Session.Create(spammer.Id, "refresh-hash", DateTimeOffset.UtcNow.AddDays(7), "User-Agent", "127.0.0.1");

        await dbContext.Users.AddRangeAsync(reporter, spammer);
        await dbContext.Sessions.AddAsync(session);
        await dbContext.Reports.AddAsync(report);
        await dbContext.SaveChangesAsync();

        var moderatorId = Guid.NewGuid();
        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(report.Id, moderatorId, "SUSPEND_USER", "Excessive spam");
        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.Action.Should().Be("SUSPEND_USER");
        response.Status.Should().Be("RESOLVED");

        var dbSpammer = await dbContext.Users.FindAsync(spammer.Id);
        dbSpammer!.IsSuspended.Should().BeTrue();
        dbSpammer.SuspensionReason.Should().Be("Excessive spam");

        var dbSession = await dbContext.Sessions.FindAsync(session.Id);
        dbSession!.RevokedAt.Should().NotBeNull();

        var auditLog = await dbContext.ModerationAuditLogs.FirstOrDefaultAsync(a => a.TargetId == spammer.Id);
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("USER_SUSPENDED");

        var outboxEvent = await dbContext.OutboxEvents.FirstOrDefaultAsync(e => e.EventType == "UserSuspended");
        outboxEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_SuspendUser_WhenTargetIsAdmin_ShouldThrowForbidden()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var admin = User.Create("superadmin", "admin@test.com", "hash", "Super Admin", role: UserRole.ADMIN);
        var report = Report.CreateForUser(reporter.Id, admin.Id, ReportCategory.HARASSMENT, "Report against admin");

        await dbContext.Users.AddRangeAsync(reporter, admin);
        await dbContext.Reports.AddAsync(report);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(report.Id, Guid.NewGuid(), "SUSPEND_USER", "Attempt to suspend admin");
        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Administrators cannot be suspended*");
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenSameActionAppliedToAlreadyProcessedReport()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var targetUser = User.Create("target", "target@test.com", "hash", "Target");
        var report = Report.CreateForUser(reporter.Id, targetUser.Id, ReportCategory.SPAM, "Spam");
        report.Resolve(Guid.NewGuid());

        await dbContext.Users.AddRangeAsync(reporter, targetUser);
        await dbContext.Reports.AddAsync(report);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(report.Id, Guid.NewGuid(), "RESOLVE", "Already resolved");
        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.Status.Should().Be("RESOLVED");
        response.Message.Should().Contain("already");
    }

    [Fact]
    public async Task Handle_ShouldThrowConflict_WhenConflictingActionAppliedToProcessedReport()
    {
        using var dbContext = CreateDbContext();
        var reporter = User.Create("reporter", "reporter@test.com", "hash", "Reporter");
        var targetUser = User.Create("target", "target@test.com", "hash", "Target");
        var report = Report.CreateForUser(reporter.Id, targetUser.Id, ReportCategory.SPAM, "Spam");
        report.Dismiss(Guid.NewGuid());

        await dbContext.Users.AddRangeAsync(reporter, targetUser);
        await dbContext.Reports.AddAsync(report);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        var command = new ExecuteModerationActionCommand(report.Id, Guid.NewGuid(), "RESOLVE", "Conflicting action");
        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already been processed*");
    }

    [Fact]
    public async Task GetSuspendedUsers_ShouldReturnOnlySuspendedUsers_WithSearchAndPostsCount()
    {
        using var dbContext = CreateDbContext();
        var activeUser = User.Create("active_user", "active@test.com", "hash", "Active User");
        var suspended1 = User.Create("bad_spammer", "spam@test.com", "hash", "Spam User");
        suspended1.Suspend("Posting spam links repeatedly");
        var suspended2 = User.Create("troll_account", "troll@test.com", "hash", "Troll User");
        suspended2.Suspend("Harassing members");

        await dbContext.Users.AddRangeAsync(activeUser, suspended1, suspended2);

        // Add 2 posts for suspended1
        var post1 = Post.Create(suspended1.Id, "Spam message 1");
        var post2 = Post.Create(suspended1.Id, "Spam message 2");
        await dbContext.Posts.AddRangeAsync(post1, post2);
        await dbContext.SaveChangesAsync();

        var handler = new GetSuspendedUsersQueryHandler(dbContext, NullLogger<GetSuspendedUsersQueryHandler>.Instance);

        // 1. Fetch all suspended
        var queryAll = new GetSuspendedUsersQuery(1, 10);
        var resultAll = await handler.Handle(queryAll, CancellationToken.None);

        resultAll.TotalCount.Should().Be(2);
        resultAll.Items.Should().HaveCount(2);
        resultAll.Items.Select(x => x.Username).Should().NotContain("active_user");
        resultAll.Items.First(x => x.Username == "bad_spammer").TotalPosts.Should().Be(2);

        // 2. Search filter
        var querySearch = new GetSuspendedUsersQuery(1, 10, "troll");
        var resultSearch = await handler.Handle(querySearch, CancellationToken.None);

        resultSearch.TotalCount.Should().Be(1);
        resultSearch.Items[0].Username.Should().Be("troll_account");
    }

    [Fact]
    public async Task UnsuspendUser_ShouldReinstateUser_RestoreHiddenPosts_AndCreateAuditLog()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("reformed_user", "reformed@test.com", "hash", "Reformed");
        user.Suspend("Temporary ban");

        var post1 = Post.Create(user.Id, "Hidden post by moderation");
        post1.Hide("Spam policy");
        var post2 = Post.Create(user.Id, "Normal non-hidden post");

        await dbContext.Users.AddAsync(user);
        await dbContext.Posts.AddRangeAsync(post1, post2);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var adminId = Guid.NewGuid();
        var handler = new UnsuspendUserCommandHandler(dbContext, outbox, NullLogger<UnsuspendUserCommandHandler>.Instance);

        var command = new UnsuspendUserCommand(user.Id, adminId, "Appeal approved after review");
        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.UserId.Should().Be(user.Id);
        response.Username.Should().Be("reformed_user");

        // Verify user state
        var dbUser = await dbContext.Users.FindAsync(user.Id);
        dbUser!.IsSuspended.Should().BeFalse();
        dbUser.SuspendedAt.Should().BeNull();
        dbUser.SuspensionReason.Should().BeNull();

        // Verify post restored
        var dbPost1 = await dbContext.Posts.FindAsync(post1.Id);
        dbPost1!.IsHidden.Should().BeFalse();
        dbPost1.HiddenAt.Should().BeNull();
        dbPost1.HiddenReason.Should().BeNull();

        // Verify audit log
        var audit = await dbContext.ModerationAuditLogs.FirstOrDefaultAsync(a => a.TargetId == user.Id && a.Action == "USER_UNSUSPENDED");
        audit.Should().NotBeNull();
        audit!.ActorUserId.Should().Be(adminId);
        audit.Reason.Should().Be("Appeal approved after review");

        // Verify outbox
        var outboxEvent = await dbContext.OutboxEvents.FirstOrDefaultAsync(e => e.EventType == "UserReinstated" && e.AggregateId == user.Id);
        outboxEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldResolveAllStackedPendingReports_WhenActingOnPostReport()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("badauthor", "badauthor@test.com", "hash", "Bad Author");
        var rep1 = User.Create("rep1", "rep1@test.com", "hash", "Reporter 1");
        var rep2 = User.Create("rep2", "rep2@test.com", "hash", "Reporter 2");
        var post = Post.Create(author.Id, "Spam and abusive content");

        await dbContext.Users.AddRangeAsync(author, rep1, rep2);
        await dbContext.Posts.AddAsync(post);

        var report1 = Report.CreateForPost(rep1.Id, post.Id, ReportCategory.SPAM, "Spam link");
        var report2 = Report.CreateForPost(rep2.Id, post.Id, ReportCategory.HARASSMENT, "Harassment violation");

        await dbContext.Reports.AddRangeAsync(report1, report2);
        await dbContext.SaveChangesAsync();

        var outbox = new OutboxWriter(dbContext);
        var modId = Guid.NewGuid();
        var handler = new ExecuteModerationActionCommandHandler(dbContext, outbox, NullLogger<ExecuteModerationActionCommandHandler>.Instance);

        // Moderator hides the post via report1
        var command = new ExecuteModerationActionCommand(report1.Id, modId, "HIDE_POST", "Violating community standards");
        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.Action.Should().Be("HIDE_POST");

        // Verify post is hidden
        var dbPost = await dbContext.Posts.FindAsync(post.Id);
        dbPost!.IsHidden.Should().BeTrue();

        // Verify BOTH reports are resolved atomically
        var dbReport1 = await dbContext.Reports.FindAsync(report1.Id);
        var dbReport2 = await dbContext.Reports.FindAsync(report2.Id);

        dbReport1!.Status.Should().Be(ReportStatus.RESOLVED);
        dbReport2!.Status.Should().Be(ReportStatus.RESOLVED);
    }

    [Fact]
    public async Task GetReports_ShouldGroupReportsByTarget_AndReturnReportCountAndDetails()
    {
        using var dbContext = CreateDbContext();
        var author = User.Create("author_spammer", "author_spammer@test.com", "hash", "Spammer");
        var rep1 = User.Create("whistle1", "whistle1@test.com", "hash", "Whistle 1");
        var rep2 = User.Create("whistle2", "whistle2@test.com", "hash", "Whistle 2");
        var post = Post.Create(author.Id, "Multiple reported post content");

        await dbContext.Users.AddRangeAsync(author, rep1, rep2);
        await dbContext.Posts.AddAsync(post);

        var report1 = Report.CreateForPost(rep1.Id, post.Id, ReportCategory.SPAM, "Commercial spam");
        var report2 = Report.CreateForPost(rep2.Id, post.Id, ReportCategory.INAPPROPRIATE_CONTENT, "Inappropriate image");

        await dbContext.Reports.AddRangeAsync(report1, report2);
        await dbContext.SaveChangesAsync();

        var handler = new GetReportsQueryHandler(dbContext);
        var result = await handler.Handle(new GetReportsQuery(Status: "PENDING"), CancellationToken.None);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);

        var stackedItem = result.Items[0];
        stackedItem.TargetPostId.Should().Be(post.Id);
        stackedItem.ReportCount.Should().Be(2);
        stackedItem.Categories.Should().Contain("SPAM");
        stackedItem.Categories.Should().Contain("INAPPROPRIATE_CONTENT");
        // Must NOT overwrite the card with the new report's data
        stackedItem.ReporterUsername.Should().Be("whistle1");
        stackedItem.Reason.Should().Be("Commercial spam");
        stackedItem.Category.Should().Be("SPAM");
        // Must show all reports in details
        stackedItem.Reports.Should().HaveCount(2);
        stackedItem.Reports![0].ReporterUsername.Should().Be("whistle1");
        stackedItem.Reports[1].ReporterUsername.Should().Be("whistle2");
    }
}
