using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Trending;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Observability;

namespace CEBAS.Api.Features.Trending.AggregateTrendingTopics;

/// <summary>
/// Implements the real-time sliding window trending algorithm using Redis Sorted Sets and exponential time decay.
/// Uses rolling 24-hour hourly buckets with formula: TrendingScore = Sum( count_in_bucket * e^(-lambda * age_in_hours) ).
/// </summary>
public class TrendingService : ITrendingService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<TrendingOptions> _options;
    private readonly ILogger<TrendingService> _logger;

    private const string AggregatedCacheKey = "trending:aggregated:top";
    private const string InMemoryCacheKey = "trending:top:memory_cache";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TrendingService(
        IConnectionMultiplexer redis,
        IMemoryCache memoryCache,
        IOptions<TrendingOptions> options,
        ILogger<TrendingService> logger)
    {
        _redis = redis;
        _memoryCache = memoryCache;
        _options = options;
        _logger = logger;
    }

    public async Task<TrendingTopicsResult> GetTopTrendsAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        var clampedLimit = Math.Clamp(limit, 1, 50);

        try
        {
            if (_redis.IsConnected)
            {
                var db = _redis.GetDatabase();

                // 1. Try reading pre-aggregated result from Redis cache
                var cachedJson = await db.StringGetAsync(AggregatedCacheKey);
                if (cachedJson.HasValue && !cachedJson.IsNullOrEmpty)
                {
                    var cachedResult = JsonSerializer.Deserialize<TrendingTopicsResult>(cachedJson.ToString(), JsonOpts);
                    if (cachedResult != null && cachedResult.Items.Count > 0)
                    {
                        var items = cachedResult.Items.Take(clampedLimit).ToList();
                        return new TrendingTopicsResult(items);
                    }
                }
            }

            // 2. Check local in-memory fallback cache
            if (_memoryCache.TryGetValue(InMemoryCacheKey, out TrendingTopicsResult? memCached) && memCached != null)
            {
                var items = memCached.Items.Take(clampedLimit).ToList();
                return new TrendingTopicsResult(items);
            }

            // 3. Compute dynamically if cache missed and Redis is connected
            if (_redis.IsConnected)
            {
                var computed = await ComputeTrendsInternalAsync(cancellationToken);
                var items = computed.Items.Take(clampedLimit).ToList();
                return new TrendingTopicsResult(items);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "trending.aggregation.failed: Failed to retrieve trending topics from Redis");
        }

        // 4. Return in-memory fallback if available, otherwise graceful empty state
        if (_memoryCache.TryGetValue(InMemoryCacheKey, out TrendingTopicsResult? fallback) && fallback != null)
        {
            return new TrendingTopicsResult(fallback.Items.Take(clampedLimit).ToList());
        }

        return new TrendingTopicsResult(Array.Empty<TrendingTopicDto>());
    }

    public async Task PrecomputeAndCacheTrendsAsync(CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected)
        {
            _logger.LogWarning("trending.precompute.skipped: Redis is not connected");
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await ComputeTrendsInternalAsync(cancellationToken);
            sw.Stop();
            TrendingMetrics.CalculationDuration.Record(sw.Elapsed.TotalMilliseconds);

            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(result, JsonOpts);

            // Cache for 2x aggregation interval (e.g. 120 seconds)
            var cacheTtl = TimeSpan.FromSeconds(Math.Max(30, _options.Value.AggregationIntervalSeconds * 2));
            await db.StringSetAsync(AggregatedCacheKey, json, cacheTtl);

            // Also keep in memory
            _memoryCache.Set(InMemoryCacheKey, result, TimeSpan.FromMinutes(5));

            _logger.LogDebug("trending.precompute.succeeded: Precomputed {Count} top trending topics in {ElapsedMs}ms",
                result.Items.Count, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "trending.precompute.failed: Error precomputing trending topics");
        }
    }

    public async Task CleanupExpiredBucketsAsync(CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected) return;

        var sw = Stopwatch.StartNew();
        try
        {
            var db = _redis.GetDatabase();
            var retentionHours = Math.Max(24, _options.Value.RetentionHours);
            var now = DateTimeOffset.UtcNow;

            // Deterministic targeted cleanup for hours outside retention window (look back 24 hours past retention)
            for (int offset = retentionHours + 1; offset <= retentionHours + 24; offset++)
            {
                var oldHour = now.AddHours(-offset).ToString("yyyyMMddHH");
                var bucketKey = $"trending:hashtags:{oldHour}";
                var dedupKey = $"trending:dedup:{oldHour}";

                await db.KeyDeleteAsync(bucketKey);
                await db.KeyDeleteAsync(dedupKey);
            }

            sw.Stop();
            TrendingMetrics.CleanupDuration.Record(sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "trending.cleanup.failed: Error cleaning up expired Redis trending buckets");
        }
    }

    private async Task<TrendingTopicsResult> ComputeTrendsInternalAsync(CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var windowHours = Math.Clamp(_options.Value.WindowHours, 1, 168); // Max 7 days
        var lambda = _options.Value.DecayLambda > 0 ? _options.Value.DecayLambda : 0.05;
        var now = DateTimeOffset.UtcNow;

        var tagAccumulator = new Dictionary<string, (double DecayedScore, long TotalPostCount)>(StringComparer.Ordinal);
        int bucketsInspected = 0;

        for (int age = 0; age < windowHours; age++)
        {
            var hourTime = now.AddHours(-age);
            var bucketKey = $"trending:hashtags:{hourTime:yyyyMMddHH}";
            var weight = Math.Exp(-lambda * age);

            var exists = await db.KeyExistsAsync(bucketKey);
            if (!exists) continue;

            bucketsInspected++;
            var entries = await db.SortedSetRangeByRankWithScoresAsync(bucketKey, 0, -1, Order.Descending);

            foreach (var entry in entries)
            {
                var tag = entry.Element.ToString();
                var rawScore = entry.Score;
                if (string.IsNullOrWhiteSpace(tag) || rawScore <= 0) continue;

                var postCount = (long)rawScore;
                var decayedScore = rawScore * weight;

                if (tagAccumulator.TryGetValue(tag, out var current))
                {
                    tagAccumulator[tag] = (current.DecayedScore + decayedScore, current.TotalPostCount + postCount);
                }
                else
                {
                    tagAccumulator[tag] = (decayedScore, postCount);
                }
            }
        }

        TrendingMetrics.RedisBucketCount.Add(bucketsInspected);

        // Deterministic ordering: highest score DESC, then post count DESC, then tag ASC (stable tie-breaker)
        var maxLimit = Math.Max(50, _options.Value.ResultLimit);
        var rankedItems = tagAccumulator
            .OrderByDescending(kv => kv.Value.DecayedScore)
            .ThenByDescending(kv => kv.Value.TotalPostCount)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(maxLimit)
            .Select((kv, index) => new TrendingTopicDto(
                Rank: index + 1,
                Tag: kv.Key,
                Score: Math.Round(kv.Value.DecayedScore, 2),
                PostCount: kv.Value.TotalPostCount
            ))
            .ToList();

        var result = new TrendingTopicsResult(rankedItems);

        // Update in-memory cache
        _memoryCache.Set(InMemoryCacheKey, result, TimeSpan.FromMinutes(2));

        return result;
    }
}
