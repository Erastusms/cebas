namespace CEBAS.Application.Contracts.Media;

public record CreateMediaUploadRequest(
    string FileName,
    string ContentType,
    long FileSize
);

public record CreateMediaUploadResponse(
    Guid MediaId,
    string UploadUrl,
    string Method,
    IDictionary<string, string> Headers,
    DateTimeOffset ExpiresAt
);

public record MediaResponse(
    Guid Id,
    Guid OwnerUserId,
    string OriginalFileName,
    string StorageKey,
    string MimeType,
    long FileSize,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt = null,
    string? Url = null
);

public record UpdateAvatarRequest(
    Guid MediaId
);
