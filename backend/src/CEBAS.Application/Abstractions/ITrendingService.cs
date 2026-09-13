using CEBAS.Application.Contracts.Trending;

namespace CEBAS.Application.Abstractions;

/// <summary>
/// Service responsible for calculating real-time trending topics over a rolling 24-hour window
/// with exponential time-decay scoring, managing the aggregation cache, and performing targeted bucket cleanup.
/// </summary>
public interface ITrendingService
{
    Task<TrendingTopicsResult> GetTopTrendsAsync(int limit = 10, CancellationToken cancellationToken = default);
    Task PrecomputeAndCacheTrendsAsync(CancellationToken cancellationToken = default);
    Task CleanupExpiredBucketsAsync(CancellationToken cancellationToken = default);
}
