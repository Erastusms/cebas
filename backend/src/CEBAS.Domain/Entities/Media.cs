using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing uploaded binary media metadata and lifecycle tracking.
/// </summary>
public class Media : Entity
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    public static readonly string[] AllowedImageMimeTypes = ["image/jpeg", "image/png", "image/webp"];

    public Guid OwnerUserId { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public MediaStatus Status { get; private set; } = MediaStatus.Uploading;
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation properties
    public User? OwnerUser { get; private set; }

    // EF Core parameterless constructor
    protected Media() { }

    public static Media Create(
        Guid ownerUserId,
        string originalFileName,
        string storageKey,
        string mimeType,
        long fileSize)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ValidationException("OwnerUserId", "Owner user ID cannot be empty.");
        }

        var trimmedFileName = originalFileName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedFileName) || trimmedFileName.Length > 255)
        {
            throw new ValidationException("OriginalFileName", "Original file name must be between 1 and 255 characters.");
        }

        var trimmedStorageKey = storageKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedStorageKey) || trimmedStorageKey.Length > 500)
        {
            throw new ValidationException("StorageKey", "Storage key must be between 1 and 500 characters.");
        }

        var normalizedMime = mimeType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedImageMimeTypes.Contains(normalizedMime))
        {
            throw new ValidationException("MimeType", $"Unsupported media type '{mimeType}'. Allowed types: {string.Join(", ", AllowedImageMimeTypes)}.");
        }

        if (fileSize <= 0 || fileSize > MaxFileSizeBytes)
        {
            throw new ValidationException("FileSize", $"File size must be between 1 byte and {MaxFileSizeBytes} bytes (5 MB).");
        }

        var media = new Media
        {
            Id = Uuid7.New(),
            OwnerUserId = ownerUserId,
            OriginalFileName = trimmedFileName,
            StorageKey = trimmedStorageKey,
            MimeType = normalizedMime,
            FileSize = fileSize,
            Status = MediaStatus.Uploading,
            CreatedAt = DateTimeOffset.UtcNow
        };

        media.AddDomainEvent(new Events.MediaUploadInitiatedDomainEvent(
            media.Id,
            media.OwnerUserId,
            media.StorageKey,
            media.CreatedAt));

        return media;
    }

    public void Confirm()
    {
        if (Status == MediaStatus.Ready)
        {
            // Idempotent confirmation
            return;
        }

        if (Status != MediaStatus.Uploading)
        {
            throw new ValidationException("Status", $"Cannot confirm media in status '{Status}'. Only media in 'Uploading' status can be confirmed.");
        }

        Status = MediaStatus.Ready;
        ConfirmedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new Events.MediaUploadConfirmedDomainEvent(
            Id,
            OwnerUserId,
            StorageKey,
            ConfirmedAt.Value));
    }

    public void Fail()
    {
        Status = MediaStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDeleted()
    {
        Status = MediaStatus.Deleted;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
