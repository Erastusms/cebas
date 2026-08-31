namespace CEBAS.Application.Abstractions;

public record StorageObjectDescriptor(
    string StorageKey,
    string MimeType,
    long FileSize,
    TimeSpan Expiration
);

public record UploadTarget(
    string StorageKey,
    string UploadUrl,
    string HttpMethod,
    IDictionary<string, string> Headers,
    DateTimeOffset ExpiresAt
);

public record StoredObjectMetadata(
    string StorageKey,
    long ContentLength,
    string ContentType,
    DateTimeOffset LastModified
);

public interface IObjectStorage
{
    Task<UploadTarget> CreateUploadTargetAsync(
        StorageObjectDescriptor descriptor,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<StoredObjectMetadata?> GetMetadataAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string storageKey,
        Stream content,
        string mimeType,
        CancellationToken cancellationToken = default);
}
