namespace CEBAS.Application.Contracts.SocialGraph;

public record FollowResponse(
    Guid TargetUserId,
    bool IsFollowing,
    bool IsBlocked
);

public record BlockResponse(
    Guid TargetUserId,
    bool IsBlocked,
    bool IsFollowing
);

public record SocialUserDto(
    Guid Id,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    bool IsVerified,
    DateTimeOffset FollowedAt,
    Guid FollowId,
    bool IsFollowing,
    bool IsFollowedBy,
    bool IsBlocked
);
