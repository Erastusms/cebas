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

public sealed record BannerUpdatedDomainEvent(
    Guid UserId,
    Guid? MediaId,
    string? BannerUrl,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record UserFollowedDomainEvent(
    Guid FollowerId,
    Guid FollowingId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record UserUnfollowedDomainEvent(
    Guid FollowerId,
    Guid FollowingId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record UserBlockedDomainEvent(
    Guid BlockerId,
    Guid BlockedId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record UserUnblockedDomainEvent(
    Guid BlockerId,
    Guid BlockedId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record FollowRelationshipsRemovedByBlockDomainEvent(
    Guid BlockerId,
    Guid BlockedId,
    int FollowsRemovedCount,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PostCreatedDomainEvent(
    Guid PostId,
    Guid AuthorId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PostDeletedDomainEvent(
    Guid PostId,
    Guid AuthorId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record ReplyCreatedDomainEvent(
    Guid ReplyId,
    Guid PostId,
    Guid AuthorId,
    Guid? ParentReplyId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record ReplyDeletedDomainEvent(
    Guid ReplyId,
    Guid PostId,
    Guid AuthorId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PostLikedDomainEvent(
    Guid PostId,
    Guid ActorUserId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PostUnlikedDomainEvent(
    Guid PostId,
    Guid ActorUserId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PostBookmarkedDomainEvent(
    Guid PostId,
    Guid ActorUserId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PostUnbookmarkedDomainEvent(
    Guid PostId,
    Guid ActorUserId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record ReportCreatedDomainEvent(
    Guid ReportId,
    Guid ReporterUserId,
    Guid? TargetPostId,
    Guid? TargetUserId,
    string Category,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record ReportResolvedDomainEvent(
    Guid ReportId,
    Guid ModeratorUserId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record ReportDismissedDomainEvent(
    Guid ReportId,
    Guid ModeratorUserId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PostHiddenDomainEvent(
    Guid PostId,
    Guid AuthorId,
    string Reason,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record PostRestoredDomainEvent(
    Guid PostId,
    Guid AuthorId,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record UserSuspendedDomainEvent(
    Guid UserId,
    string Reason,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record UserReinstatedDomainEvent(
    Guid UserId,
    DateTimeOffset OccurredAt
) : IDomainEvent;


