using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Media;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Media.Upload;

public sealed record CreateMediaUploadCommand(
    Guid UserId,
    string FileName,
    string ContentType,
    long FileSize
) : IRequest<CreateMediaUploadResponse>;

public sealed class CreateMediaUploadCommandValidator : AbstractValidator<CreateMediaUploadCommand>
{
    public CreateMediaUploadCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name cannot be empty.")
            .MaximumLength(255).WithMessage("File name cannot exceed 255 characters.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type cannot be empty.")
            .Must(ct => Domain.Entities.Media.AllowedImageMimeTypes.Contains(ct.Trim().ToLowerInvariant()))
            .WithMessage($"Unsupported content type. Allowed types: {string.Join(", ", Domain.Entities.Media.AllowedImageMimeTypes)}.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("File size must be greater than 0 bytes.")
            .LessThanOrEqualTo(Domain.Entities.Media.MaxFileSizeBytes)
            .WithMessage($"File size cannot exceed {Domain.Entities.Media.MaxFileSizeBytes} bytes (5 MB).");
    }
}

public sealed class CreateMediaUploadCommandHandler : IRequestHandler<CreateMediaUploadCommand, CreateMediaUploadResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IObjectStorage _objectStorage;
    private readonly MediaStorageOptions _options;
    private readonly ILogger<CreateMediaUploadCommandHandler> _logger;

    public CreateMediaUploadCommandHandler(
        ApplicationDbContext dbContext,
        IObjectStorage objectStorage,
        IOptions<MediaStorageOptions> options,
        ILogger<CreateMediaUploadCommandHandler> logger)
    {
        _dbContext = dbContext;
        _objectStorage = objectStorage;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CreateMediaUploadResponse> Handle(CreateMediaUploadCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedException("Authenticated user could not be found.");
        }

        var normalizedMime = request.ContentType.Trim().ToLowerInvariant();
        var extension = normalizedMime switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin"
        };

        // Canonical username directory component (alphanumeric and underscores only)
        var sanitizedUsername = Regex.Replace(user.Username.ToLowerInvariant(), @"[^a-z0-9_]", "");
        if (string.IsNullOrEmpty(sanitizedUsername))
        {
            sanitizedUsername = "user_" + user.Id.ToString("N");
        }

        // Generate safe unique storage key: media/{username}/{mediaId}.{ext}
        var mediaId = Domain.Common.Uuid7.New();
        var storageKey = $"media/{sanitizedUsername}/{mediaId}{extension}";

        var media = Domain.Entities.Media.Create(
            user.Id,
            request.FileName,
            storageKey,
            normalizedMime,
            request.FileSize);

        await _dbContext.Media.AddAsync(media, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Media upload initiated [MediaId: {MediaId}, StorageKey: {StorageKey}, Owner: @{Username}]",
            media.Id, storageKey, user.Username);

        var expiration = TimeSpan.FromMinutes(_options.UploadUrlExpirationMinutes > 0 ? _options.UploadUrlExpirationMinutes : 15);
        var descriptor = new StorageObjectDescriptor(storageKey, normalizedMime, request.FileSize, expiration);
        var target = await _objectStorage.CreateUploadTargetAsync(descriptor, cancellationToken);

        return new CreateMediaUploadResponse(
            media.Id,
            target.UploadUrl,
            target.HttpMethod,
            target.Headers,
            target.ExpiresAt);
    }
}

public sealed class MediaUploadInitiatedEventHandler : INotificationHandler<MediaUploadInitiatedDomainEvent>
{
    private readonly ILogger<MediaUploadInitiatedEventHandler> _logger;

    public MediaUploadInitiatedEventHandler(ILogger<MediaUploadInitiatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(MediaUploadInitiatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] MediaUploadInitiated: MediaId {MediaId}, OwnerUserId {OwnerUserId}, StorageKey '{StorageKey}' at {OccurredAt}",
            notification.MediaId, notification.OwnerUserId, notification.StorageKey, notification.OccurredAt);
        return Task.CompletedTask;
    }
}
