using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using CEBAS.Application.Abstractions;

namespace CEBAS.Infrastructure.Services;

/// <summary>
/// Distributed sliding-window rate limiter using Redis sorted sets (ZSET) via atomic Lua script execution.
/// Provides resilient in-memory sliding-window fallback when Redis is temporarily offline or unreachable.
/// </summary>
public class RedisRateLimiterService : IRateLimiterService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimiterService> _logger;

    // Fallback thread-safe in-memory sliding window queue map for offline resilience
    private readonly ConcurrentDictionary<string, Queue<long>> _fallbackQueues = new();

    private const string SlidingWindowLuaScript = @"
        local key = KEYS[1]
        local now = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local limit = tonumber(ARGV[3])
        local member = ARGV[4]
        local clearBefore = now - window

        redis.call('ZREMRANGEBYSCORE', key, '-inf', clearBefore)
        local currentRequests = redis.call('ZCARD', key)

        if currentRequests < limit then
            redis.call('ZADD', key, now, member)
            redis.call('PEXPIRE', key, window + 1000)
            return {1, limit - currentRequests - 1, 0}
        else
            local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
            local retryAfterMs = 1000
            if oldest and #oldest >= 2 then
                retryAfterMs = math.max(1000, tonumber(oldest[2]) + window - now)
            end
            return {0, 0, math.ceil(retryAfterMs / 1000)}
        end
    ";

    public RedisRateLimiterService(
        IConnectionMultiplexer redis,
        ILogger<RedisRateLimiterService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Rate limit key cannot be empty.", nameof(key));
        }

        if (permitLimit <= 0)
        {
            return new RateLimitResult(false, 0, (int)Math.Ceiling(window.TotalSeconds));
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long windowMs = (long)window.TotalMilliseconds;
        string memberId = $"{nowMs}_{Guid.NewGuid():N}";

        try
        {
            if (_redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                var redisResult = (RedisResult[]?)await db.ScriptEvaluateAsync(
                    SlidingWindowLuaScript,
                    [new RedisKey(key)],
                    [new RedisValue(nowMs.ToString()), new RedisValue(windowMs.ToString()), new RedisValue(permitLimit.ToString()), new RedisValue(memberId)]
                );

                if (redisResult != null && redisResult.Length >= 3)
                {
                    bool allowed = (int)redisResult[0] == 1;
                    int remaining = (int)redisResult[1];
                    int retryAfterSeconds = (int)redisResult[2];

                    return new RateLimitResult(allowed, remaining, retryAfterSeconds);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis rate limiting error for key '{Key}'. Falling back to local in-memory sliding window.", key);
        }

        // Resilient fallback to local in-memory sliding window
        return FallbackInMemory(key, permitLimit, window, nowMs, windowMs);
    }

    private RateLimitResult FallbackInMemory(string key, int permitLimit, TimeSpan window, long nowMs, long windowMs)
    {
        var queue = _fallbackQueues.GetOrAdd(key, _ => new Queue<long>());

        lock (queue)
        {
            while (queue.Count > 0 && queue.Peek() < (nowMs - windowMs))
            {
                queue.Dequeue();
            }

            if (queue.Count < permitLimit)
            {
                queue.Enqueue(nowMs);
                int remaining = Math.Max(0, permitLimit - queue.Count);
                return new RateLimitResult(true, remaining, 0);
            }

            long oldest = queue.Peek();
            long retryAfterMs = Math.Max(1000, oldest + windowMs - nowMs);
            int retryAfterSeconds = (int)Math.Ceiling(retryAfterMs / 1000.0);

            return new RateLimitResult(false, 0, retryAfterSeconds);
        }
    }
}
