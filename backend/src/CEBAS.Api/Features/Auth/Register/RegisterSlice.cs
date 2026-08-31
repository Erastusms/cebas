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

namespace CEBAS.Api.Features.Auth.Register;

public sealed record RegisterCommand(
    string Username,
    string Email,
    string Password,
    string? DisplayName = null
) : IRequest<CurrentUserResponse>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Length(3, 30).WithMessage("Username must be between 3 and 30 characters.")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Username can only contain alphanumeric characters and underscores.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(50).WithMessage("Display name cannot exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.DisplayName));
    }
}

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, CurrentUserResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        ILogger<RegisterCommandHandler> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<CurrentUserResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedUsername = IdentityNormalizers.NormalizeUsername(request.Username);
        var normalizedEmail = IdentityNormalizers.NormalizeEmail(request.Email);

        // Check username conflict (case-insensitive)
        var usernameExists = await _dbContext.Users
            .AnyAsync(u => u.Username.ToLower() == normalizedUsername, cancellationToken);
        if (usernameExists)
        {
            throw new ConflictException($"The username '{request.Username}' is already taken.");
        }

        // Check email conflict (case-insensitive)
        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var effectiveDisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? request.Username.Trim()
            : request.DisplayName.Trim();

        var user = User.Create(
            username: request.Username.Trim(),
            email: normalizedEmail,
            passwordHash: passwordHash,
            displayName: effectiveDisplayName,
            role: UserRole.User
        );

        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New user registered: @{Username} [Id: {UserId}]", user.Username, user.Id);

        return new CurrentUserResponse(
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
    }
}

public sealed class UserRegisteredEventHandler : INotificationHandler<UserRegisteredDomainEvent>
{
    private readonly ILogger<UserRegisteredEventHandler> _logger;

    public UserRegisteredEventHandler(ILogger<UserRegisteredEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserRegisteredDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] UserRegistered: @{Username} ({Email}) at {OccurredAt}",
            notification.Username, notification.Email, notification.OccurredAt);
        return Task.CompletedTask;
    }
}
