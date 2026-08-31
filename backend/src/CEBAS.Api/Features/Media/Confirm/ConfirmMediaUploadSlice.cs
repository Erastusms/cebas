using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Media;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Media.Confirm;

public sealed record ConfirmMediaUploadCommand(
    Guid UserId,
    Guid MediaId
) : IRequest<MediaResponse>;

public sealed class ConfirmMediaUploadCommandValidator : AbstractValidator<ConfirmMediaUploadCommand>
{
    public ConfirmMediaUploadCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.MediaId)
            .NotEmpty().WithMessage("Media ID is required.");
    }
}

public sealed class ConfirmMediaUploadCommandHandler : IRequestHandler<ConfirmMediaUploadCommand, MediaResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IObjectStorage _objectStorage;
    private readonly ILogger<ConfirmMediaUploadCommandHandler> _logger;

    public ConfirmMediaUploadCommandHandler(
        ApplicationDbContext dbContext,
        IObjectStorage objectStorage,
        ILogger<ConfirmMediaUploadCommandHandler> logger)
    {
        _dbContext = dbContext;
        _objectStorage = objectStorage;
        _logger = logger;
    }

    public async Task<MediaResponse> Handle(ConfirmMediaUploadCommand request, CancellationToken cancellationToken)
    {
        var media = await _dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        if (media == null)
        {
            throw new NotFoundException("Media record not found.");
        }

        // Ownership enforcement
        if (media.OwnerUserId != request.UserId)
        {
            _logger.LogWarning("User {UserId} attempted to confirm media {MediaId} owned by {OwnerUserId}",
                request.UserId, media.Id, media.OwnerUserId);
            throw new ForbiddenException("Cannot confirm media owned by another user.");
        }

        // Idempotency: if already confirmed and ready, return existing state
        if (media.Status == MediaStatus.Ready)
        {
            return new MediaResponse(
                media.Id,
                media.OwnerUserId,
                media.OriginalFileName,
                media.StorageKey,
                media.MimeType,
                media.FileSize,
                media.Status.ToString().ToUpperInvariant(),
                media.CreatedAt,
                media.ConfirmedAt,
                $"/api/v1/media/{media.Id}");
        }

        if (media.Status != MediaStatus.Uploading)
        {
            throw new ValidationException("Status", $"Cannot confirm media in status '{media.Status}'. Only 'UPLOADING' media can be confirmed.");
        }

        // Storage object verification
        var objectExists = await _objectStorage.ExistsAsync(media.StorageKey, cancellationToken);
        if (!objectExists)
        {
            _logger.LogWarning("Confirmation failed for MediaId {MediaId}: Storage key '{StorageKey}' not found in storage.",
                media.Id, media.StorageKey);
            throw new ValidationException("Storage", "Uploaded file binary was not found in storage. Ensure direct binary upload completed successfully before confirmation.");
        }

        // Transition to READY
        media.Confirm();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Media upload confirmed successfully [MediaId: {MediaId}, StorageKey: {StorageKey}]",
            media.Id, media.StorageKey);

        return new MediaResponse(
            media.Id,
            media.OwnerUserId,
            media.OriginalFileName,
            media.StorageKey,
            media.MimeType,
            media.FileSize,
            media.Status.ToString().ToUpperInvariant(),
            media.CreatedAt,
            media.ConfirmedAt,
            $"/api/v1/media/{media.Id}");
    }
}

public sealed class MediaUploadConfirmedEventHandler : INotificationHandler<MediaUploadConfirmedDomainEvent>
{
    private readonly ILogger<MediaUploadConfirmedEventHandler> _logger;

    public MediaUploadConfirmedEventHandler(ILogger<MediaUploadConfirmedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(MediaUploadConfirmedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] MediaUploadConfirmed: MediaId {MediaId}, OwnerUserId {OwnerUserId}, StorageKey '{StorageKey}' at {OccurredAt}",
            notification.MediaId, notification.OwnerUserId, notification.StorageKey, notification.OccurredAt);
        return Task.CompletedTask;
    }
}
