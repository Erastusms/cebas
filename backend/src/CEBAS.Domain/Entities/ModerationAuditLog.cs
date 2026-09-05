using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity recording sensitive administrative and moderation operations for auditability.
/// </summary>
public class ModerationAuditLog : Entity
{
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public string? Reason { get; private set; }
    public string? Metadata { get; private set; }

    // Navigation properties
    public User? ActorUser { get; private set; }

    // EF Core parameterless constructor
    protected ModerationAuditLog() { }

    public static ModerationAuditLog Create(
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        string? reason = null,
        string? metadata = null)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new ValidationException("ActorUserId", "Actor user ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ValidationException("Action", "Audit action cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(targetType))
        {
            throw new ValidationException("TargetType", "Target type cannot be empty.");
        }

        if (targetId == Guid.Empty)
        {
            throw new ValidationException("TargetId", "Target ID cannot be empty.");
        }

        return new ModerationAuditLog
        {
            Id = Uuid7.New(),
            ActorUserId = actorUserId,
            Action = action.Trim().ToUpperInvariant(),
            TargetType = targetType.Trim(),
            TargetId = targetId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Metadata = string.IsNullOrWhiteSpace(metadata) ? null : metadata.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
