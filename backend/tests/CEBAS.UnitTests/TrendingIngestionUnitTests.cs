using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using CEBAS.Api.Features.Hashtags.ExtractHashtags;
using CEBAS.Api.Features.Trending.IngestHashtagActivity;
using CEBAS.Application.Contracts.Events;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.UnitTests;

public class TrendingIngestionUnitTests
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly IOptions<TrendingOptions> _options;
    private readonly HashtagParser _parser;
    private readonly ApplicationDbContext _dbContext;

    public TrendingIngestionUnitTests()
    {
        _redis = Substitute.For<IConnectionMultiplexer>();
        _database = Substitute.For<IDatabase>();
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _redis.IsConnected.Returns(true);

        _options = Options.Create(new TrendingOptions
        {
            WindowHours = 24,
            DecayLambda = 0.05,
            ResultLimit = 10,
            RetentionHours = 48
        });

        _parser = new HashtagParser();

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TrendingIngestionDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new ApplicationDbContext(dbOptions);
    }

    [Fact]
    public async Task IngestPostCreatedAsync_NewEvent_IncrementsSortedSetAndSetsTtl()
    {
        var service = new TrendingIngestionService(_redis, _options, _parser, _dbContext, NullLogger<TrendingIngestionService>.Instance);
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _database.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true); // New event (not duplicate)

        var payload = new PostCreatedPayload(
            postId,
            authorId,
            "Hello #CEBAS #trending",
            0,
            now,
            new List<string> { "cebas", "trending" }
        );

        await service.IngestPostCreatedAsync(payload);

        // Verify SetAdd was called for both tags
        await _database.Received(1).SetAddAsync(Arg.Any<RedisKey>(), $"{postId}:cebas", Arg.Any<CommandFlags>());
        await _database.Received(1).SetAddAsync(Arg.Any<RedisKey>(), $"{postId}:trending", Arg.Any<CommandFlags>());

        // Verify SortedSetIncrement was called for both tags
        await _database.Received(1).SortedSetIncrementAsync(Arg.Any<RedisKey>(), "cebas", 1.0, Arg.Any<CommandFlags>());
        await _database.Received(1).SortedSetIncrementAsync(Arg.Any<RedisKey>(), "trending", 1.0, Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task IngestPostCreatedAsync_DuplicateEvent_DoesNotIncrementSortedSet()
    {
        var service = new TrendingIngestionService(_redis, _options, _parser, _dbContext, NullLogger<TrendingIngestionService>.Instance);
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        // Simulate duplicate event: SetAdd returns false
        _database.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(false);

        var payload = new PostCreatedPayload(
            postId,
            authorId,
            "Hello #CEBAS duplicate",
            0,
            DateTimeOffset.UtcNow,
            new List<string> { "cebas" }
        );

        await service.IngestPostCreatedAsync(payload);

        // Dedup check was performed
        await _database.Received(1).SetAddAsync(Arg.Any<RedisKey>(), $"{postId}:cebas", Arg.Any<CommandFlags>());

        // SortedSet was NOT incremented!
        await _database.DidNotReceive().SortedSetIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<double>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task IngestPostCreatedAsync_WhenRedisDisconnected_FailsGracefullyWithoutException()
    {
        _redis.IsConnected.Returns(false);
        var service = new TrendingIngestionService(_redis, _options, _parser, _dbContext, NullLogger<TrendingIngestionService>.Instance);

        var payload = new PostCreatedPayload(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Post with #CEBAS",
            0,
            DateTimeOffset.UtcNow,
            new List<string> { "cebas" }
        );

        // Should not throw
        var act = () => service.IngestPostCreatedAsync(payload);
        await act.Should().NotThrowAsync();
    }
}
