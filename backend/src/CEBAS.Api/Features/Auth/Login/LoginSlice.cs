using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Common;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Auth.Login;

public sealed record LoginCommand(
    string Identifier,
    string Password,
    string? UserAgent = null,
    string? IpAddress = null
) : IRequest<LoginResult>;

public sealed record LoginResult(
    CurrentUserResponse User,
    string RawSessionToken,
    DateTimeOffset SessionExpiresAt
);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage("Username or email is required.")
            .MaximumLength(255).WithMessage("Identifier cannot exceed 255 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.");
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionTokenService _tokenService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        ISessionTokenService tokenService,
        ILogger<LoginCommandHandler> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalized = request.Identifier.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized || u.Email.ToLower() == normalized, cancellationToken);

        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for identifier '{Identifier}' from IP '{Ip}'",
                request.Identifier, request.IpAddress ?? "unknown");
            throw new UnauthorizedException("Invalid username/email or password.");
        }

        // Generate 256-bit cryptographically secure raw token and hash with SHA-256 for DB
        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.ComputeTokenHash(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var session = Session.Create(
            userId: user.Id,
            tokenHash: tokenHash,
            expiresAt: expiresAt,
            userAgent: request.UserAgent,
            ipAddress: request.IpAddress
        );

        await _dbContext.Sessions.AddAsync(session, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successful login for @{Username} [SessionId: {SessionId}]", user.Username, session.Id);

        var userResponse = new CurrentUserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.BannerUrl,
            user.Role.ToString().ToUpperInvariant(),
            user.IsVerified,
            user.CreatedAt,
            user.UpdatedAt
        );

        return new LoginResult(userResponse, rawToken, expiresAt);
    }
}

public sealed class UserLoggedInEventHandler : INotificationHandler<UserLoggedInDomainEvent>
{
    private readonly ILogger<UserLoggedInEventHandler> _logger;

    public UserLoggedInEventHandler(ILogger<UserLoggedInEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserLoggedInDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] UserLoggedIn: UserId {UserId}, SessionId {SessionId} from IP {Ip} at {OccurredAt}",
            notification.UserId, notification.SessionId, notification.IpAddress ?? "unknown", notification.OccurredAt);
        return Task.CompletedTask;
    }
}
