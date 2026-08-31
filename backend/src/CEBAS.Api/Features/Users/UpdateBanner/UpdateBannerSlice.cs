using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Users.UpdateBanner;

public sealed record UpdateBannerCommand(
    Guid UserId,
    Guid? MediaId = null,
    string? BannerUrl = null
) : IRequest<CurrentUserResponse>;

public sealed class UpdateBannerCommandValidator : AbstractValidator<UpdateBannerCommand>
{
    public UpdateBannerCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x)
            .Must(x => x.MediaId.HasValue || !string.IsNullOrWhiteSpace(x.BannerUrl))
            .WithMessage("Either media ID or banner URL must be provided.");

        RuleFor(x => x.BannerUrl)
            .MaximumLength(500).WithMessage("Banner URL cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.BannerUrl));
    }
}

public sealed class UpdateBannerCommandHandler : IRequestHandler<UpdateBannerCommand, CurrentUserResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UpdateBannerCommandHandler> _logger;

    public UpdateBannerCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<UpdateBannerCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CurrentUserResponse> Handle(UpdateBannerCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User profile not found.");
        }

        if (request.MediaId.HasValue)
        {
            var media = await _dbContext.Media
                .FirstOrDefaultAsync(m => m.Id == request.MediaId.Value, cancellationToken);

            if (media == null)
            {
                throw new NotFoundException("Media record not found.");
            }

            user.UpdateBanner(media);
        }
        else
        {
            user.SetBannerUrl(request.BannerUrl);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Banner updated for @{Username} [UserId: {UserId}, MediaId: {MediaId}, BannerUrl: {BannerUrl}]",
            user.Username, user.Id, request.MediaId, user.BannerUrl);

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

public sealed class BannerUpdatedEventHandler : INotificationHandler<BannerUpdatedDomainEvent>
{
    private readonly ILogger<BannerUpdatedEventHandler> _logger;

    public BannerUpdatedEventHandler(ILogger<BannerUpdatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(BannerUpdatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] BannerUpdated: UserId {UserId}, MediaId {MediaId}, BannerUrl {BannerUrl} at {OccurredAt}",
            notification.UserId, notification.MediaId, notification.BannerUrl, notification.OccurredAt);
        return Task.CompletedTask;
    }
}
