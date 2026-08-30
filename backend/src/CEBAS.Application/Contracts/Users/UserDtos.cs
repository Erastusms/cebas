namespace CEBAS.Application.Contracts.Users;

public record UserProfileStats(
    int PostCount,
    int FollowerCount,
    int FollowingCount
);

public record UserProfileResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    bool IsVerified,
    DateTimeOffset CreatedAt,
    UserProfileStats Stats
);

public record CurrentUserResponse(
    Guid Id,
    string Username,
    string Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string Role,
    bool IsVerified,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt = null,
    Guid? SessionId = null
);

public record UpdateProfileRequest(
    string DisplayName,
    string? Bio
);

public record SessionItemResponse(
    Guid Id,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent
);
