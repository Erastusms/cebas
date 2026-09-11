using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Users.UpdateProfile;

public sealed record UpdateProfileCommand(
    Guid UserId,
    string? DisplayName = null,
    string? Bio = null,
    string? BannerUrl = null,
    string? ThemePreference = null
) : IRequest<CurrentUserResponse>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    private static readonly HashSet<string> AllowedThemePreferences = new(StringComparer.OrdinalIgnoreCase)
    {
        "LIGHT",
        "DARK",
        "SYSTEM"
    };

    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.DisplayName != null || x.Bio != null || x.BannerUrl != null || x.ThemePreference != null)
            .WithMessage("At least one profile field must be provided for update.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name cannot be empty.")
            .Length(1, 50).WithMessage("Display name must be between 1 and 50 characters.")
            .When(x => x.DisplayName != null);

        RuleFor(x => x.Bio)
            .MaximumLength(160).WithMessage("Biography cannot exceed 160 characters.")
            .When(x => !string.IsNullOrEmpty(x.Bio));

        RuleFor(x => x.BannerUrl)
            .MaximumLength(500).WithMessage("Banner URL cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.BannerUrl));

        RuleFor(x => x.ThemePreference)
            .Must(t => t != null && AllowedThemePreferences.Contains(t.Trim()))
            .WithMessage("Theme preference must be one of: LIGHT, DARK, SYSTEM.")
            .When(x => x.ThemePreference != null);
    }
}

public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, CurrentUserResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UpdateProfileCommandHandler> _logger;

    public UpdateProfileCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<UpdateProfileCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CurrentUserResponse> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User profile not found.");
        }

        if (request.DisplayName != null)
        {
            user.UpdateProfile(request.DisplayName, request.Bio, bannerUrl: request.BannerUrl);
        }
        else if (request.Bio != null || request.BannerUrl != null)
        {
            user.UpdateProfile(user.DisplayName, request.Bio, bannerUrl: request.BannerUrl);
        }

        if (!string.IsNullOrWhiteSpace(request.ThemePreference))
        {
            if (Enum.TryParse<ThemePreference>(request.ThemePreference.Trim(), true, out var theme) && Enum.IsDefined(theme))
            {
                user.UpdateThemePreference(theme);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated for @{Username} [UserId: {UserId}, Theme: {Theme}]", user.Username, user.Id, user.ThemePreference);

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
            user.UpdatedAt,
            null,
            user.ThemePreference.ToString().ToUpperInvariant()
        );
    }
}

public sealed class ProfileUpdatedEventHandler : INotificationHandler<ProfileUpdatedDomainEvent>
{
    private readonly ILogger<ProfileUpdatedEventHandler> _logger;

    public ProfileUpdatedEventHandler(ILogger<ProfileUpdatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ProfileUpdatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] ProfileUpdated: UserId {UserId}, DisplayName '{DisplayName}' at {OccurredAt}",
            notification.UserId, notification.DisplayName, notification.OccurredAt);
        return Task.CompletedTask;
    }
}
