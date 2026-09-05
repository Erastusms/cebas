using FluentAssertions;
using Xunit;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;

namespace CEBAS.UnitTests;

public class ReportEntityUnitTests
{
    private readonly Guid _reporterId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _moderatorId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidPostTarget_ShouldSucceed()
    {
        var report = Report.Create(
            _reporterId,
            _postId,
            null,
            ReportCategory.SPAM,
            "Spam promotional content."
        );

        report.Should().NotBeNull();
        report.Id.Should().NotBeEmpty();
        report.ReporterUserId.Should().Be(_reporterId);
        report.TargetPostId.Should().Be(_postId);
        report.TargetUserId.Should().BeNull();
        report.Category.Should().Be(ReportCategory.SPAM);
        report.Status.Should().Be(ReportStatus.PENDING);
        report.Reason.Should().Be("Spam promotional content.");
        report.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Create_WithValidUserTarget_ShouldSucceed()
    {
        var report = Report.Create(
            _reporterId,
            null,
            _userId,
            ReportCategory.HARASSMENT,
            "Targeted harassment account."
        );

        report.Should().NotBeNull();
        report.TargetPostId.Should().BeNull();
        report.TargetUserId.Should().Be(_userId);
        report.Category.Should().Be(ReportCategory.HARASSMENT);
        report.Status.Should().Be(ReportStatus.PENDING);
    }

    [Fact]
    public void Create_WithBothTargets_ShouldThrowValidationException()
    {
        var act = () => Report.Create(
            _reporterId,
            _postId,
            _userId,
            ReportCategory.HATE_SPEECH
        );

        var ex = act.Should().Throw<ValidationException>().Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*either a post or a user*");
    }

    [Fact]
    public void Create_WithNeitherTarget_ShouldThrowValidationException()
    {
        var act = () => Report.Create(
            _reporterId,
            null,
            null,
            ReportCategory.INAPPROPRIATE_CONTENT
        );

        var ex = act.Should().Throw<ValidationException>().Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*either a post or a user*");
    }

    [Fact]
    public void Create_WithEmptyReporterId_ShouldThrowValidationException()
    {
        var act = () => Report.Create(
            Guid.Empty,
            _postId,
            null,
            ReportCategory.SPAM
        );

        var ex = act.Should().Throw<ValidationException>().Which;
        ex.Errors.Values.SelectMany(v => v).Should().ContainMatch("*Reporter user ID*");
    }

    [Fact]
    public void Resolve_WhenPending_ShouldTransitionToResolved()
    {
        var report = Report.Create(_reporterId, _postId, null, ReportCategory.SPAM);

        report.Resolve(_moderatorId);

        report.Status.Should().Be(ReportStatus.RESOLVED);
        report.ResolvedAt.Should().NotBeNull();
        report.ResolvedByUserId.Should().Be(_moderatorId);
    }

    [Fact]
    public void Dismiss_WhenPending_ShouldTransitionToDismissed()
    {
        var report = Report.Create(_reporterId, null, _userId, ReportCategory.HARASSMENT);

        report.Dismiss(_moderatorId);

        report.Status.Should().Be(ReportStatus.DISMISSED);
        report.ResolvedAt.Should().NotBeNull();
        report.ResolvedByUserId.Should().Be(_moderatorId);
    }

    [Fact]
    public void Resolve_WhenAlreadyResolved_ShouldThrowConflictException()
    {
        var report = Report.Create(_reporterId, _postId, null, ReportCategory.SPAM);
        report.Resolve(_moderatorId);

        var act = () => report.Resolve(_moderatorId);

        act.Should().Throw<ConflictException>()
            .WithMessage("*already RESOLVED*");
    }

    [Fact]
    public void Dismiss_WhenAlreadyDismissed_ShouldThrowConflictException()
    {
        var report = Report.Create(_reporterId, null, _userId, ReportCategory.SPAM);
        report.Dismiss(_moderatorId);

        var act = () => report.Dismiss(_moderatorId);

        act.Should().Throw<ConflictException>()
            .WithMessage("*already DISMISSED*");
    }

    [Fact]
    public void User_SuspendAndReinstate_ShouldUpdateStateAndEmitDomainEvents()
    {
        var user = User.Create("testuser", "test@example.com", "hash", "Test User");

        user.IsSuspended.Should().BeFalse();

        user.Suspend("Repeated violations of terms");
        user.IsSuspended.Should().BeTrue();
        user.SuspendedAt.Should().NotBeNull();
        user.SuspensionReason.Should().Be("Repeated violations of terms");

        // Idempotent suspend
        user.Suspend("Another reason");
        user.SuspensionReason.Should().Be("Repeated violations of terms");

        user.Reinstate();
        user.IsSuspended.Should().BeFalse();
        user.SuspendedAt.Should().BeNull();
        user.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public void Post_HideAndRestore_ShouldUpdateStateAndEmitDomainEvents()
    {
        var post = Post.Create(Guid.NewGuid(), "Violating content");

        post.IsHidden.Should().BeFalse();

        post.Hide("Spam content");
        post.IsHidden.Should().BeTrue();
        post.HiddenAt.Should().NotBeNull();
        post.HiddenReason.Should().Be("Spam content");

        // Idempotent hide
        post.Hide("Another reason");
        post.HiddenReason.Should().Be("Spam content");

        post.Restore();
        post.IsHidden.Should().BeFalse();
        post.HiddenAt.Should().BeNull();
        post.HiddenReason.Should().BeNull();
    }
}
