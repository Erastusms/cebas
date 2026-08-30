using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Users.UpdateProfile;

public sealed record UpdateProfileCommand(
    Guid UserId,
    string DisplayName,
    string? Bio
) : IRequest<CurrentUserResponse>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name cannot be empty.")
            .Length(1, 50).WithMessage("Display name must be between 1 and 50 characters.");

        RuleFor(x => x.Bio)
            .MaximumLength(160).WithMessage("Biography cannot exceed 160 characters.")
            .When(x => !string.IsNullOrEmpty(x.Bio));
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

        user.UpdateProfile(request.DisplayName, request.Bio);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated for @{Username} [UserId: {UserId}]", user.Username, user.Id);

        return new CurrentUserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.Role.ToString().ToUpperInvariant(),
            user.IsVerified,
            user.CreatedAt
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
