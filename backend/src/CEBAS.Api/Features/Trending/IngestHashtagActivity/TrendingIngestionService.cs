using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Events;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Trending.IngestHashtagActivity;

/// <summary>
/// Implements asynchronous hashtag activity ingestion into Redis hourly buckets with idempotency protection.
/// Ensures resilient operation so Redis downtime does not fail core post creation workflows.
/// </summary>
public class TrendingIngestionService : ITrendingIngestionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IOptions<TrendingOptions> _options;
    private readonly IHashtagParser _hashtagParser;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<TrendingIngestionService> _logger;

    public TrendingIngestionService(
        IConnectionMultiplexer redis,
        IOptions<TrendingOptions> options,
        IHashtagParser hashtagParser,
        ApplicationDbContext dbContext,
        ILogger<TrendingIngestionService> logger)
    {
        _redis = redis;
        _options = options;
        _hashtagParser = hashtagParser;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task IngestPostCreatedAsync(PostCreatedPayload payload, CancellationToken cancellationToken = default)
    {
        if (payload == null) return;

        // Resolve normalized hashtags from payload or parse from content
        List<string> tags;
        if (payload.Hashtags != null && payload.Hashtags.Count > 0)
        {
            tags = payload.Hashtags
                .Select(t => _hashtagParser.Normalize(t))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
        }
        else if (!string.IsNullOrWhiteSpace(payload.Content))
        {
            tags = _hashtagParser.ExtractHashtags(payload.Content)
                .Select(h => h.NormalizedName)
                .Distinct()
                .ToList();
        }
        else
        {
            return;
        }

        if (tags.Count == 0) return;

        try
        {
            if (!_redis.IsConnected)
            {
                _logger.LogWarning("trending.redis.unavailable: Redis multiplexer is disconnected. Skipping ingestion for post {PostId}", payload.PostId);
                TrendingMetrics.RedisIncrementFailure.Add(tags.Count);
                return;
            }

            var db = _redis.GetDatabase();
            var createdAt = payload.CreatedAt == default ? DateTimeOffset.UtcNow : payload.CreatedAt;
            var bucketHour = createdAt.ToUniversalTime().ToString("yyyyMMddHH");

            var bucketKey = $"trending:hashtags:{bucketHour}";
            var dedupKey = $"trending:dedup:{bucketHour}";
            var retentionTtl = TimeSpan.FromHours(Math.Max(1, _options.Value.RetentionHours));

            foreach (var tag in tags)
            {
                // Invariant: One eligible post + One hashtag + One hourly bucket = One logical increment
                var dedupMember = $"{payload.PostId}:{tag}";
                bool isNew = await db.SetAddAsync(dedupKey, dedupMember);

                if (isNew)
                {
                    await db.SortedSetIncrementAsync(bucketKey, tag, 1.0);
                    await db.KeyExpireAsync(bucketKey, retentionTtl, ExpireWhen.Always);
                    await db.KeyExpireAsync(dedupKey, retentionTtl, ExpireWhen.Always);

                    TrendingMetrics.RedisIncrementSuccess.Add(1);
                    TrendingMetrics.HashtagsIngestionCount.Add(1);
                }
                else
                {
                    TrendingMetrics.DuplicateEventsCount.Add(1);
                    _logger.LogDebug("trending.dedup: Duplicate activity suppressed for post {PostId} and tag {Tag} in bucket {BucketHour}",
                        payload.PostId, tag, bucketHour);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "trending.redis.increment.failed: Error ingesting hashtag activity for post {PostId}", payload.PostId);
            TrendingMetrics.RedisIncrementFailure.Add(tags.Count);
        }
    }

    public async Task ReconcilePostDeletedAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected) return;

            // Look up post and its associated hashtags
            var post = await _dbContext.Posts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);

            if (post == null) return;

            var hashtags = await _dbContext.PostHashtags
                .AsNoTracking()
                .Where(ph => ph.PostId == postId)
                .Select(ph => ph.Hashtag!.NormalizedName)
                .ToListAsync(cancellationToken);

            if (hashtags.Count == 0) return;

            var db = _redis.GetDatabase();
            var bucketHour = post.CreatedAt.ToUniversalTime().ToString("yyyyMMddHH");
            var bucketKey = $"trending:hashtags:{bucketHour}";
            var dedupKey = $"trending:dedup:{bucketHour}";

            foreach (var tag in hashtags)
            {
                var dedupMember = $"{postId}:{tag}";
                bool removed = await db.SetRemoveAsync(dedupKey, dedupMember);
                if (removed)
                {
                    var newScore = await db.SortedSetDecrementAsync(bucketKey, tag, 1.0);
                    if (newScore <= 0)
                    {
                        await db.SortedSetRemoveAsync(bucketKey, tag);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "trending.reconcile.deleted.failed: Failed to reconcile deleted post {PostId} in Redis", postId);
        }
    }
}
