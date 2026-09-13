using CEBAS.Application.Contracts.Events;

namespace CEBAS.Application.Abstractions;

/// <summary>
/// Service responsible for asynchronously ingesting hashtag activity into Redis hourly buckets
/// and maintaining idempotent duplicate event protection.
/// </summary>
public interface ITrendingIngestionService
{
    Task IngestPostCreatedAsync(PostCreatedPayload payload, CancellationToken cancellationToken = default);
    Task ReconcilePostDeletedAsync(Guid postId, CancellationToken cancellationToken = default);
}
