namespace CEBAS.Application.Contracts.Users;

public record UserProfileStats(
    int PostCount,
    int FollowerCount,
    int FollowingCount
);

public record UserProfileRelationship(
    bool IsFollowing,
    bool IsFollowedBy,
    bool IsBlocked,
    bool IsBlockedBy
);

public record UserProfileResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? BannerUrl,
    bool IsVerified,
    DateTimeOffset CreatedAt,
    UserProfileStats Stats,
    UserProfileRelationship? Relationship = null
);

public record CurrentUserResponse(
    Guid Id,
    string Username,
    string Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? BannerUrl,
    string Role,
    bool IsVerified,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt = null,
    Guid? SessionId = null
);

public record UpdateProfileRequest(
    string DisplayName,
    string? Bio,
    string? BannerUrl = null
);

public record UpdateBannerRequest(
    Guid? MediaId = null,
    string? BannerUrl = null
);

public record SessionItemResponse(
    Guid Id,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent
);
