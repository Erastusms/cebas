using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Users.UpdateAvatar;

public sealed record UpdateAvatarCommand(
    Guid UserId,
    Guid MediaId
) : IRequest<CurrentUserResponse>;

public sealed class UpdateAvatarCommandValidator : AbstractValidator<UpdateAvatarCommand>
{
    public UpdateAvatarCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.MediaId)
            .NotEmpty().WithMessage("Media ID is required.");
    }
}

public sealed class UpdateAvatarCommandHandler : IRequestHandler<UpdateAvatarCommand, CurrentUserResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UpdateAvatarCommandHandler> _logger;

    public UpdateAvatarCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<UpdateAvatarCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CurrentUserResponse> Handle(UpdateAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User profile not found.");
        }

        var media = await _dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        if (media == null)
        {
            throw new NotFoundException("Media record not found.");
        }

        // Domain validation: ownership, ready status, supported image mime type
        user.UpdateAvatar(media);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Avatar updated for @{Username} [UserId: {UserId}, MediaId: {MediaId}]",
            user.Username, user.Id, media.Id);

        return new CurrentUserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.Role.ToString().ToUpperInvariant(),
            user.IsVerified,
            user.CreatedAt,
            user.UpdatedAt
        );
    }
}

public sealed class AvatarUpdatedEventHandler : INotificationHandler<AvatarUpdatedDomainEvent>
{
    private readonly ILogger<AvatarUpdatedEventHandler> _logger;

    public AvatarUpdatedEventHandler(ILogger<AvatarUpdatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(AvatarUpdatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] AvatarUpdated: UserId {UserId}, MediaId {MediaId} at {OccurredAt}",
            notification.UserId, notification.MediaId, notification.OccurredAt);
        return Task.CompletedTask;
    }
}
