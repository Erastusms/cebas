using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Reports;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Safety.Moderation.ExecuteModerationAction;

public sealed record ExecuteModerationActionCommand(
    Guid ReportId,
    Guid ModeratorUserId,
    string Action,
    string? Reason
) : IRequest<ModerationActionResponse>;

public sealed class ExecuteModerationActionCommandValidator : AbstractValidator<ExecuteModerationActionCommand>
{
    public ExecuteModerationActionCommandValidator()
    {
        RuleFor(x => x.ReportId)
            .NotEmpty().WithMessage("Report ID is required.");

        RuleFor(x => x.ModeratorUserId)
            .NotEmpty().WithMessage("Moderator user ID is required.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Moderation action is required.")
            .Must(BeValidAction).WithMessage("Invalid action. Allowed values: RESOLVE, DISMISS, HIDE_POST, SUSPEND_USER.");

        RuleFor(x => x.Reason)
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters.");
    }

    private static bool BeValidAction(string action)
    {
        var normalized = action?.Trim().ToUpperInvariant();
        return normalized is "RESOLVE" or "DISMISS" or "HIDE_POST" or "SUSPEND_USER";
    }
}

public sealed class ExecuteModerationActionCommandHandler : IRequestHandler<ExecuteModerationActionCommand, ModerationActionResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<ExecuteModerationActionCommandHandler> _logger;

    public ExecuteModerationActionCommandHandler(
        ApplicationDbContext dbContext,
        IOutboxWriter outboxWriter,
        ILogger<ExecuteModerationActionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task<ModerationActionResponse> Handle(ExecuteModerationActionCommand request, CancellationToken cancellationToken)
    {
        var report = await _dbContext.Reports
            .Include(r => r.TargetPost)
            .Include(r => r.TargetUser)
            .FirstOrDefaultAsync(r => r.Id == request.ReportId, cancellationToken);

        if (report == null)
        {
            throw new NotFoundException($"Report with ID '{request.ReportId}' was not found.");
        }

        string normalizedAction = request.Action.Trim().ToUpperInvariant();
        string message;
        var now = DateTimeOffset.UtcNow;

        // Idempotency check: if report already in terminal state
        if (report.Status != ReportStatus.PENDING)
        {
            if ((normalizedAction == "RESOLVE" && report.Status == ReportStatus.RESOLVED) ||
                (normalizedAction == "DISMISS" && report.Status == ReportStatus.DISMISSED))
            {
                return new ModerationActionResponse(
                    report.Id,
                    normalizedAction,
                    report.Status.ToString(),
                    $"Report was already {report.Status}.",
                    now
                );
            }

            throw new ConflictException($"Report '{report.Id}' has already been processed with status '{report.Status}'.");
        }

        // Find any other pending reports targeting the same post or user (stacked reports)
        List<Report> relatedPendingReports;
        if (report.TargetPostId.HasValue)
        {
            relatedPendingReports = await _dbContext.Reports
                .Where(r => r.TargetPostId == report.TargetPostId && r.Status == ReportStatus.PENDING && r.Id != report.Id)
                .ToListAsync(cancellationToken);
        }
        else if (report.TargetUserId.HasValue)
        {
            relatedPendingReports = await _dbContext.Reports
                .Where(r => r.TargetUserId == report.TargetUserId && r.Status == ReportStatus.PENDING && r.Id != report.Id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            relatedPendingReports = new List<Report>();
        }

        switch (normalizedAction)
        {
            case "RESOLVE":
                report.Resolve(request.ModeratorUserId);
                foreach (var rel in relatedPendingReports)
                {
                    rel.Resolve(request.ModeratorUserId);
                }
                message = relatedPendingReports.Count > 0
                    ? $"Report and {relatedPendingReports.Count} other stacked report(s) resolved successfully."
                    : "Report resolved successfully.";

                var resolveAudit = ModerationAuditLog.Create(
                    request.ModeratorUserId,
                    "REPORT_RESOLVED",
                    "Report",
                    report.Id,
                    request.Reason
                );
                await _dbContext.ModerationAuditLogs.AddAsync(resolveAudit, cancellationToken);

                await _outboxWriter.EnqueueAsync(
                    "ReportResolved",
                    "Report",
                    report.Id,
                    new { report.Id, ModeratorId = request.ModeratorUserId, Timestamp = now },
                    actorId: request.ModeratorUserId,
                    cancellationToken: cancellationToken);
                break;

            case "DISMISS":
                report.Dismiss(request.ModeratorUserId);
                foreach (var rel in relatedPendingReports)
                {
                    rel.Dismiss(request.ModeratorUserId);
                }
                message = relatedPendingReports.Count > 0
                    ? $"Report and {relatedPendingReports.Count} other stacked report(s) dismissed successfully."
                    : "Report dismissed successfully.";

                var dismissAudit = ModerationAuditLog.Create(
                    request.ModeratorUserId,
                    "REPORT_DISMISSED",
                    "Report",
                    report.Id,
                    request.Reason
                );
                await _dbContext.ModerationAuditLogs.AddAsync(dismissAudit, cancellationToken);

                await _outboxWriter.EnqueueAsync(
                    "ReportDismissed",
                    "Report",
                    report.Id,
                    new { report.Id, ModeratorId = request.ModeratorUserId, Timestamp = now },
                    actorId: request.ModeratorUserId,
                    cancellationToken: cancellationToken);
                break;

            case "HIDE_POST":
                if (!report.TargetPostId.HasValue || report.TargetPost == null)
                {
                    throw new ValidationException("Action", "HIDE_POST action is only valid for reports targeting a post.");
                }

                report.TargetPost.Hide(request.Reason ?? "Violation of community guidelines.");
                report.Resolve(request.ModeratorUserId);
                foreach (var rel in relatedPendingReports)
                {
                    rel.Resolve(request.ModeratorUserId);
                }
                message = relatedPendingReports.Count > 0
                    ? $"Violating post has been hidden and {relatedPendingReports.Count + 1} report(s) resolved."
                    : "Violating post has been hidden and report resolved.";

                var hideAudit = ModerationAuditLog.Create(
                    request.ModeratorUserId,
                    "POST_HIDDEN",
                    "Post",
                    report.TargetPost.Id,
                    request.Reason
                );
                await _dbContext.ModerationAuditLogs.AddAsync(hideAudit, cancellationToken);

                await _outboxWriter.EnqueueAsync(
                    "PostHidden",
                    "Post",
                    report.TargetPost.Id,
                    new { PostId = report.TargetPost.Id, report.TargetPost.AuthorId, Reason = request.Reason, Timestamp = now },
                    actorId: request.ModeratorUserId,
                    cancellationToken: cancellationToken);
                break;

            case "SUSPEND_USER":
                Guid targetUserId = report.TargetUserId ?? report.TargetPost?.AuthorId ?? Guid.Empty;
                if (targetUserId == Guid.Empty)
                {
                    throw new ValidationException("Action", "Could not identify target user to suspend from this report.");
                }

                var userToSuspend = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
                if (userToSuspend == null)
                {
                    throw new NotFoundException($"Target user with ID '{targetUserId}' was not found.");
                }

                if (userToSuspend.Role is UserRole.Admin or UserRole.ADMIN)
                {
                    throw new ForbiddenException("Administrators cannot be suspended through community moderation actions.");
                }

                userToSuspend.Suspend(request.Reason ?? "Violation of platform community standards.");

                // Revoke all active sessions of the suspended user
                var activeSessions = await _dbContext.Sessions
                    .Where(s => s.UserId == targetUserId && s.RevokedAt == null)
                    .ToListAsync(cancellationToken);

                foreach (var session in activeSessions)
                {
                    session.Revoke(now);
                }

                report.Resolve(request.ModeratorUserId);
                foreach (var rel in relatedPendingReports)
                {
                    rel.Resolve(request.ModeratorUserId);
                }
                message = $"User @{userToSuspend.Username} has been suspended and {activeSessions.Count} active session(s) revoked.";

                var suspendAudit = ModerationAuditLog.Create(
                    request.ModeratorUserId,
                    "USER_SUSPENDED",
                    "User",
                    userToSuspend.Id,
                    request.Reason
                );
                await _dbContext.ModerationAuditLogs.AddAsync(suspendAudit, cancellationToken);

                await _outboxWriter.EnqueueAsync(
                    "UserSuspended",
                    "User",
                    userToSuspend.Id,
                    new { UserId = userToSuspend.Id, Username = userToSuspend.Username, Reason = request.Reason, Timestamp = now },
                    actorId: request.ModeratorUserId,
                    cancellationToken: cancellationToken);
                break;

            default:
                throw new ValidationException("Action", $"Unsupported moderation action '{request.Action}'.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Moderation action {Action} executed by {ModeratorUserId} on report {ReportId}",
            normalizedAction, request.ModeratorUserId, report.Id);

        return new ModerationActionResponse(
            report.Id,
            normalizedAction,
            report.Status.ToString(),
            message,
            now
        );
    }
}
