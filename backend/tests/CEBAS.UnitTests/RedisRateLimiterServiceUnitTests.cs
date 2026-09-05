using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using CEBAS.Infrastructure.Services;

namespace CEBAS.UnitTests;

public class RedisRateLimiterServiceUnitTests
{
    [Fact]
    public async Task CheckRateLimitAsync_ShouldThrowArgumentException_WhenKeyIsNullOrWhitespace()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        var service = new RedisRateLimiterService(redis, NullLogger<RedisRateLimiterService>.Instance);

        var actNull = () => service.CheckRateLimitAsync(null!, 10, TimeSpan.FromSeconds(60));
        await actNull.Should().ThrowAsync<ArgumentException>();

        var actEmpty = () => service.CheckRateLimitAsync("   ", 10, TimeSpan.FromSeconds(60));
        await actEmpty.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CheckRateLimitAsync_ShouldDisallowImmediately_WhenPermitLimitIsZeroOrNegative()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        var service = new RedisRateLimiterService(redis, NullLogger<RedisRateLimiterService>.Instance);

        var result = await service.CheckRateLimitAsync("test:key", 0, TimeSpan.FromSeconds(30));

        result.IsAllowed.Should().BeFalse();
        result.RemainingPermits.Should().Be(0);
        result.RetryAfterSeconds.Should().Be(30);
    }

    [Fact]
    public async Task CheckRateLimitAsync_FallbackInMemory_ShouldAllowWithinLimitAndThrottleWhenExceeded()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.IsConnected.Returns(false); // Force in-memory fallback

        var service = new RedisRateLimiterService(redis, NullLogger<RedisRateLimiterService>.Instance);
        var key = $"fallback:test:{Guid.NewGuid()}";
        var window = TimeSpan.FromSeconds(10);
        int limit = 3;

        // 1st request -> allowed
        var r1 = await service.CheckRateLimitAsync(key, limit, window);
        r1.IsAllowed.Should().BeTrue();
        r1.RemainingPermits.Should().Be(2);

        // 2nd request -> allowed
        var r2 = await service.CheckRateLimitAsync(key, limit, window);
        r2.IsAllowed.Should().BeTrue();
        r2.RemainingPermits.Should().Be(1);

        // 3rd request -> allowed
        var r3 = await service.CheckRateLimitAsync(key, limit, window);
        r3.IsAllowed.Should().BeTrue();
        r3.RemainingPermits.Should().Be(0);

        // 4th request -> blocked!
        var r4 = await service.CheckRateLimitAsync(key, limit, window);
        r4.IsAllowed.Should().BeFalse();
        r4.RemainingPermits.Should().Be(0);
        r4.RetryAfterSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CheckRateLimitAsync_FallbackInMemory_ShouldHandleConcurrentRequestsSafely()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.IsConnected.Returns(false);

        var service = new RedisRateLimiterService(redis, NullLogger<RedisRateLimiterService>.Instance);
        var key = $"concurrent:test:{Guid.NewGuid()}";
        var window = TimeSpan.FromSeconds(10);
        int limit = 50;
        int totalRequests = 100;

        int allowedCount = 0;
        int blockedCount = 0;

        await Parallel.ForEachAsync(Enumerable.Range(0, totalRequests), async (_, _) =>
        {
            var res = await service.CheckRateLimitAsync(key, limit, window);
            if (res.IsAllowed)
            {
                Interlocked.Increment(ref allowedCount);
            }
            else
            {
                Interlocked.Increment(ref blockedCount);
            }
        });

        allowedCount.Should().Be(limit);
        blockedCount.Should().Be(totalRequests - limit);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenRedisThrowsException_ShouldFallBackToInMemory()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.IsConnected.Returns(true);
        var db = Substitute.For<IDatabase>();
        db.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>())
            .Returns<RedisResult>(_ => throw new InvalidOperationException("Redis down"));
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        var service = new RedisRateLimiterService(redis, NullLogger<RedisRateLimiterService>.Instance);
        var key = $"redis-failure:test:{Guid.NewGuid()}";

        // Should not throw, should fall back to memory
        var result = await service.CheckRateLimitAsync(key, 5, TimeSpan.FromSeconds(10));
        result.IsAllowed.Should().BeTrue();
        result.RemainingPermits.Should().Be(4);
    }
}
