using System.Diagnostics.Metrics;

namespace CEBAS.Infrastructure.Observability;

/// <summary>
/// Observability metrics for hashtag extraction and real-time trending calculation engine (Phase 12).
/// </summary>
public static class TrendingMetrics
{
    public const string MeterName = "CEBAS.Trending";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> HashtagsExtractedCount = Meter.CreateCounter<long>(
        "hashtags.extracted.count",
        description: "Total count of hashtags extracted from posts");

    public static readonly Histogram<double> HashtagsExtractionDuration = Meter.CreateHistogram<double>(
        "hashtags.extraction.duration",
        unit: "ms",
        description: "Duration of hashtag extraction execution in milliseconds");

    public static readonly Counter<long> HashtagsIngestionCount = Meter.CreateCounter<long>(
        "hashtags.ingestion.count",
        description: "Total count of hashtag activity ingested into Redis");

    public static readonly Counter<long> RedisIncrementSuccess = Meter.CreateCounter<long>(
        "trending.redis.increment.success",
        description: "Successful Redis ZINCRBY operations for trending topics");

    public static readonly Counter<long> RedisIncrementFailure = Meter.CreateCounter<long>(
        "trending.redis.increment.failure",
        description: "Failed Redis trending increment operations");

    public static readonly Histogram<double> CalculationDuration = Meter.CreateHistogram<double>(
        "trending.calculation.duration",
        unit: "ms",
        description: "Duration of sliding window time-decay trending calculation in milliseconds");

    public static readonly Histogram<double> CleanupDuration = Meter.CreateHistogram<double>(
        "trending.cleanup.duration",
        unit: "ms",
        description: "Duration of expired Redis bucket cleanup in milliseconds");

    public static readonly Counter<long> RedisBucketCount = Meter.CreateCounter<long>(
        "trending.redis.bucket.count",
        description: "Number of hourly Redis buckets inspected during trending aggregation");

    public static readonly Histogram<double> ApiDuration = Meter.CreateHistogram<double>(
        "trending.api.duration",
        unit: "ms",
        description: "Response latency of GET /api/v1/trends endpoint in milliseconds");

    public static readonly Histogram<double> TagTimelineDuration = Meter.CreateHistogram<double>(
        "tag.timeline.duration",
        unit: "ms",
        description: "Response latency of GET /api/v1/timelines/tags/{tag} endpoint in milliseconds");

    public static readonly Counter<long> DuplicateEventsCount = Meter.CreateCounter<long>(
        "trending.duplicate_events.count",
        description: "Duplicate events suppressed by trending idempotency filter");
}
