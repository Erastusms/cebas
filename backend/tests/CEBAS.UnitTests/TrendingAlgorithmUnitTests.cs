using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using CEBAS.Api.Features.Trending.AggregateTrendingTopics;
using CEBAS.Infrastructure.Configuration;

namespace CEBAS.UnitTests;

public class TrendingAlgorithmUnitTests
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<TrendingOptions> _options;

    public TrendingAlgorithmUnitTests()
    {
        _redis = Substitute.For<IConnectionMultiplexer>();
        _database = Substitute.For<IDatabase>();
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _redis.IsConnected.Returns(true);

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _options = Options.Create(new TrendingOptions
        {
            WindowHours = 24,
            DecayLambda = 0.05,
            ResultLimit = 10,
            RetentionHours = 48,
            AggregationIntervalSeconds = 60
        });
    }

    [Fact]
    public async Task GetTopTrends_CurrentHourAndOlderHour_WeightsCorrectly()
    {
        var service = new TrendingService(_redis, _memoryCache, _options, NullLogger<TrendingService>.Instance);
        var now = DateTimeOffset.UtcNow;

        var currentHourKey = $"trending:hashtags:{now:yyyyMMddHH}";
        var pastHourKey = $"trending:hashtags:{now.AddHours(-10):yyyyMMddHH}";

        // Mock KeyExists
        _database.KeyExistsAsync(currentHourKey).Returns(true);
        _database.KeyExistsAsync(pastHourKey).Returns(true);

        // Tag "recent" has 10 posts in current hour (weight = e^0 = 1.0 -> score = 10.0)
        _database.SortedSetRangeByRankWithScoresAsync(currentHourKey, 0, -1, Order.Descending)
            .Returns(new[] { new SortedSetEntry("recent", 10.0) });

        // Tag "old" has 10 posts 10 hours ago (weight = e^(-0.05 * 10) = e^(-0.5) ~ 0.6065 -> score ~ 6.07)
        _database.SortedSetRangeByRankWithScoresAsync(pastHourKey, 0, -1, Order.Descending)
            .Returns(new[] { new SortedSetEntry("old", 10.0) });

        var result = await service.GetTopTrendsAsync(10);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);

        // "recent" should rank #1 due to higher time-decay score
        result.Items[0].Rank.Should().Be(1);
        result.Items[0].Tag.Should().Be("recent");
        result.Items[0].Score.Should().Be(10.0);
        result.Items[0].PostCount.Should().Be(10);

        result.Items[1].Rank.Should().Be(2);
        result.Items[1].Tag.Should().Be("old");
        result.Items[1].Score.Should().BeApproximately(6.07, 0.05);
        result.Items[1].PostCount.Should().Be(10);
    }

    [Fact]
    public async Task GetTopTrends_24HourBoundary_ExcludesBucketsOlderThanWindow()
    {
        var service = new TrendingService(_redis, _memoryCache, _options, NullLogger<TrendingService>.Instance);
        var now = DateTimeOffset.UtcNow;

        var hour23Key = $"trending:hashtags:{now.AddHours(-23):yyyyMMddHH}";
        var hour24Key = $"trending:hashtags:{now.AddHours(-24):yyyyMMddHH}";

        _database.KeyExistsAsync(hour23Key).Returns(true);
        _database.KeyExistsAsync(hour24Key).Returns(true);

        _database.SortedSetRangeByRankWithScoresAsync(hour23Key, 0, -1, Order.Descending)
            .Returns(new[] { new SortedSetEntry("inside_window", 5.0) });

        _database.SortedSetRangeByRankWithScoresAsync(hour24Key, 0, -1, Order.Descending)
            .Returns(new[] { new SortedSetEntry("outside_window", 500.0) });

        var result = await service.GetTopTrendsAsync(10);

        result.Items.Should().ContainSingle(i => i.Tag == "inside_window");
        result.Items.Should().NotContain(i => i.Tag == "outside_window");
    }

    [Fact]
    public async Task GetTopTrends_DeterministicTieBreaking_OrdersByScoreThenPostCountThenAlphabeticalTag()
    {
        var service = new TrendingService(_redis, _memoryCache, _options, NullLogger<TrendingService>.Instance);
        var now = DateTimeOffset.UtcNow;
        var currentHourKey = $"trending:hashtags:{now:yyyyMMddHH}";

        _database.KeyExistsAsync(currentHourKey).Returns(true);

        // Multiple tags with same score
        _database.SortedSetRangeByRankWithScoresAsync(currentHourKey, 0, -1, Order.Descending)
            .Returns(new[]
            {
                new SortedSetEntry("zebra", 5.0),
                new SortedSetEntry("apple", 5.0),
                new SortedSetEntry("mango", 5.0)
            });

        var result = await service.GetTopTrendsAsync(10);

        result.Items.Should().HaveCount(3);
        // Alphabetical tie-breaker: apple, mango, zebra
        result.Items.Select(i => i.Tag).Should().Equal("apple", "mango", "zebra");
        result.Items.Select(i => i.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task GetTopTrends_ZeroActivity_ReturnsEmptyItems()
    {
        var service = new TrendingService(_redis, _memoryCache, _options, NullLogger<TrendingService>.Instance);
        var result = await service.GetTopTrendsAsync(10);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopTrends_WhenRedisDisconnected_ReturnsEmptyGracefullyWithoutThrowing()
    {
        _redis.IsConnected.Returns(false);
        var service = new TrendingService(_redis, _memoryCache, _options, NullLogger<TrendingService>.Instance);

        var result = await service.GetTopTrendsAsync(10);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }
}
