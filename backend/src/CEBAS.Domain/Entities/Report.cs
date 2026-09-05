using CEBAS.Domain.Common;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a user-submitted safety/abuse report targeting either a post or a user.
/// </summary>
public class Report : Entity
{
    public const int MaxReasonLength = 1000;

    public Guid ReporterUserId { get; private set; }
    public Guid? TargetPostId { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public ReportCategory Category { get; private set; }
    public ReportStatus Status { get; private set; } = ReportStatus.PENDING;
    public string? Reason { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }

    // Navigation properties
    public User? Reporter { get; private set; }
    public Post? TargetPost { get; private set; }
    public User? TargetUser { get; private set; }
    public User? ResolvedByUser { get; private set; }

    // EF Core parameterless constructor
    protected Report() { }

    public static Report Create(
        Guid reporterUserId,
        Guid? targetPostId,
        Guid? targetUserId,
        ReportCategory category,
        string? reason = null)
    {
        if (reporterUserId == Guid.Empty)
        {
            throw new ValidationException("ReporterUserId", "Reporter user ID cannot be empty.");
        }

        bool hasPost = targetPostId.HasValue && targetPostId.Value != Guid.Empty;
        bool hasUser = targetUserId.HasValue && targetUserId.Value != Guid.Empty;

        if (hasPost == hasUser)
        {
            throw new ValidationException("Target", "A report must target either a post or a user, but not both or none.");
        }

        var trimmedReason = reason?.Trim();
        if (trimmedReason != null && trimmedReason.Length > MaxReasonLength)
        {
            throw new ValidationException("Reason", $"Report reason cannot exceed {MaxReasonLength} characters.");
        }

        var report = new Report
        {
            Id = Uuid7.New(),
            ReporterUserId = reporterUserId,
            TargetPostId = hasPost ? targetPostId : null,
            TargetUserId = hasUser ? targetUserId : null,
            Category = category,
            Status = ReportStatus.PENDING,
            Reason = string.IsNullOrWhiteSpace(trimmedReason) ? null : trimmedReason,
            CreatedAt = DateTimeOffset.UtcNow
        };

        report.AddDomainEvent(new ReportCreatedDomainEvent(
            report.Id,
            report.ReporterUserId,
            report.TargetPostId,
            report.TargetUserId,
            report.Category.ToString(),
            report.CreatedAt));

        return report;
    }

    public static Report CreateForPost(
        Guid reporterUserId,
        Guid targetPostId,
        ReportCategory category,
        string? reason = null)
    {
        return Create(reporterUserId, targetPostId, null, category, reason);
    }

    public static Report CreateForUser(
        Guid reporterUserId,
        Guid targetUserId,
        ReportCategory category,
        string? reason = null)
    {
        return Create(reporterUserId, null, targetUserId, category, reason);
    }

    public void Resolve(Guid moderatorUserId)
    {
        if (moderatorUserId == Guid.Empty)
        {
            throw new ValidationException("ModeratorUserId", "Moderator user ID cannot be empty.");
        }

        if (Status != ReportStatus.PENDING)
        {
            throw new ConflictException($"Report '{Id}' is already {Status} and cannot be resolved again.");
        }

        Status = ReportStatus.RESOLVED;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedByUserId = moderatorUserId;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ReportResolvedDomainEvent(Id, moderatorUserId, ResolvedAt.Value));
    }

    public void Dismiss(Guid moderatorUserId)
    {
        if (moderatorUserId == Guid.Empty)
        {
            throw new ValidationException("ModeratorUserId", "Moderator user ID cannot be empty.");
        }

        if (Status != ReportStatus.PENDING)
        {
            throw new ConflictException($"Report '{Id}' is already {Status} and cannot be dismissed again.");
        }

        Status = ReportStatus.DISMISSED;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedByUserId = moderatorUserId;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ReportDismissedDomainEvent(Id, moderatorUserId, ResolvedAt.Value));
    }
}
