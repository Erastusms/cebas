using CEBAS.Domain.Common;

namespace CEBAS.Domain.Events;

public sealed record UserRegisteredDomainEvent(
    Guid UserId,
    string Username,
    string Email,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record UserLoggedInDomainEvent(
    Guid UserId,
    Guid SessionId,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record UserLoggedOutDomainEvent(
    Guid UserId,
    Guid SessionId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record ProfileUpdatedDomainEvent(
    Guid UserId,
    string DisplayName,
    string? Bio,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record SessionRevokedDomainEvent(
    Guid UserId,
    Guid SessionId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record MediaUploadInitiatedDomainEvent(
    Guid MediaId,
    Guid OwnerUserId,
    string StorageKey,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record MediaUploadConfirmedDomainEvent(
    Guid MediaId,
    Guid OwnerUserId,
    string StorageKey,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record AvatarUpdatedDomainEvent(
    Guid UserId,
    Guid MediaId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

