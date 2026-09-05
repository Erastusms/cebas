namespace CEBAS.Application.Contracts.Notifications;

public sealed record NotificationActorDto(
    Guid Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    bool IsVerified
);

public sealed record NotificationResponseDto(
    Guid Id,
    NotificationActorDto Actor,
    string Type,
    Guid? TargetId,
    string? TargetType,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt
);

public sealed record UnreadNotificationCountResponse(int UnreadCount);

public sealed record MarkNotificationReadResponse(Guid Id, bool IsRead, DateTimeOffset? ReadAt);

public sealed record MarkAllNotificationsReadResponse(int MarkedReadCount);
