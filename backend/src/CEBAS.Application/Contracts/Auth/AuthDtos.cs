namespace CEBAS.Application.Contracts.Auth;

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string? DisplayName = null
);

public record LoginRequest(
    string Identifier,
    string Password
);

public record AuthResponse(
    Guid UserId,
    string Username,
    string Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string Role,
    bool IsVerified,
    Guid SessionId,
    DateTimeOffset ExpiresAt
);

public record LoginResult(
    AuthResponse Response,
    string RawToken
);
