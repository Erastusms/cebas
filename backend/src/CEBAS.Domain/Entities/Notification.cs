using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing an activity notification for a user.
/// </summary>
public class Notification : Entity
{
    public Guid RecipientId { get; private set; }
    public Guid ActorId { get; private set; }
    public NotificationType Type { get; private set; }
    public Guid? TargetId { get; private set; }
    public string? TargetType { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public string Metadata { get; private set; } = "{}";

    // Navigation properties
    public User? Recipient { get; private set; }
    public User? Actor { get; private set; }

    // Parameterless constructor for EF Core
    protected Notification() { }

    public static Notification Create(
        Guid recipientId,
        Guid actorId,
        NotificationType type,
        Guid? targetId = null,
        string? targetType = null,
        string? metadata = null)
    {
        if (recipientId == Guid.Empty)
        {
            throw new ValidationException("RecipientId", "Recipient user ID is required.");
        }

        if (actorId == Guid.Empty)
        {
            throw new ValidationException("ActorId", "Actor user ID is required.");
        }

        if (recipientId == actorId)
        {
            throw new ValidationException("Notification", "A user cannot receive notifications from themselves.");
        }

        var now = DateTimeOffset.UtcNow;
        return new Notification
        {
            Id = Uuid7.New(),
            RecipientId = recipientId,
            ActorId = actorId,
            Type = type,
            TargetId = targetId,
            TargetType = targetType,
            IsRead = false,
            ReadAt = null,
            Metadata = string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkAsRead(DateTimeOffset now)
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = now;
            UpdatedAt = now;
        }
    }
}
