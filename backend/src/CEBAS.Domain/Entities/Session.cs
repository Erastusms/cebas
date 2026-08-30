using CEBAS.Domain.Common;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a stateful, multi-device login session.
/// Only the SHA-256 hash of the session token is persisted.
/// </summary>
public class Session : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // Navigation property
    public User? User { get; private set; }

    // EF Core parameterless constructor
    protected Session() { }

    public static Session Create(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? userAgent = null,
        string? ipAddress = null)
    {
        var session = new Session
        {
            Id = Uuid7.New(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
            IpAddress = ipAddress?.Length > 45 ? ipAddress[..45] : ipAddress,
            CreatedAt = DateTimeOffset.UtcNow
        };

        session.AddDomainEvent(new Events.UserLoggedInDomainEvent(userId, session.Id, session.IpAddress, session.UserAgent, session.CreatedAt));
        return session;
    }

    public void Revoke(DateTimeOffset utcNow)
    {
        if (RevokedAt == null)
        {
            RevokedAt = utcNow;
            UpdatedAt = utcNow;
            AddDomainEvent(new Events.SessionRevokedDomainEvent(UserId, Id, utcNow));
        }
    }

    public bool IsActive(DateTimeOffset utcNow) => RevokedAt == null && ExpiresAt > utcNow;
}
