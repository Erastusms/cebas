using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class DatabasePerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DatabasePerformanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeployedIndexes_ShouldContainAllRequiredCompositeIndexes_AndExcludeRedundantOnes()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT indexname FROM pg_indexes WHERE schemaname = 'public';";
        using var reader = await command.ExecuteReaderAsync();

        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert all required high-frequency composite & filtered indexes exist
        indexes.Should().Contain("idx_posts_author_active");
        indexes.Should().Contain("idx_posts_active_timeline_v2");
        indexes.Should().Contain("idx_post_replies_active_thread");
        indexes.Should().Contain("idx_notifications_recipient_feed");
        indexes.Should().Contain("idx_follows_following_follower");

        // Assert redundant/overlapping indexes were safely dropped
        indexes.Should().NotContain("idx_posts_created_pagination");
        indexes.Should().NotContain("idx_posts_author_id");
    }

    [Fact]
    public async Task HomeFeedQuery_ShouldAchieveP95Under50Milliseconds()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // Resolve an active viewer user
        using var userCmd = connection.CreateCommand();
        userCmd.CommandText = "SELECT id FROM users LIMIT 1;";
        var viewerIdObj = await userCmd.ExecuteScalarAsync();
        var viewerId = (Guid)viewerIdObj!;

        var latencies = new List<double>();
        const int iterations = 25;

        // Warm up
        using (var warmupCmd = connection.CreateCommand())
        {
            warmupCmd.CommandText = BuildHomeFeedSql(viewerId);
            await warmupCmd.ExecuteNonQueryAsync();
        }

        // Benchmark runs
        for (int i = 0; i < iterations; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = BuildHomeFeedSql(viewerId);

            var sw = Stopwatch.StartNew();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
            sw.Stop();

            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        var p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
        var p95Latency = latencies[p95Index];

        // Target: p95 database/query latency < 50ms for Home Feed workload
        p95Latency.Should().BeLessThan(50.0, $"Home Feed p95 latency must be under 50ms, was {p95Latency:F2}ms");
    }

    [Fact]
    public async Task ProfileTimelineQuery_ShouldAchieveP95Under50Milliseconds()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var authorCmd = connection.CreateCommand();
        authorCmd.CommandText = "SELECT author_id FROM posts GROUP BY author_id ORDER BY count(*) DESC LIMIT 1;";
        var authorIdObj = await authorCmd.ExecuteScalarAsync();
        var authorId = (Guid)authorIdObj!;

        var latencies = new List<double>();
        const int iterations = 25;

        // Warmup
        using (var warmupCmd = connection.CreateCommand())
        {
            warmupCmd.CommandText = BuildProfileTimelineSql(authorId);
            await warmupCmd.ExecuteNonQueryAsync();
        }

        for (int i = 0; i < iterations; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = BuildProfileTimelineSql(authorId);

            var sw = Stopwatch.StartNew();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
            sw.Stop();

            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        var p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
        var p95Latency = latencies[p95Index];

        // Target: p95 query latency < 50ms for Profile Timeline workload
        p95Latency.Should().BeLessThan(50.0, $"Profile Timeline p95 latency must be under 50ms, was {p95Latency:F2}ms");
    }

    [Fact]
    public async Task HighFrequencyQueries_NotificationsAndFollowers_ShouldExecuteUnder50Milliseconds()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        using var userCmd = connection.CreateCommand();
        userCmd.CommandText = "SELECT id FROM users LIMIT 1;";
        var userId = (Guid)(await userCmd.ExecuteScalarAsync())!;

        // 1. Notifications recipient feed query
        using (var notifCmd = connection.CreateCommand())
        {
            notifCmd.CommandText = $@"
                SELECT id, recipient_id, actor_id, type, target_id, target_type, is_read, created_at 
                FROM notifications 
                WHERE recipient_id = '{userId}' 
                ORDER BY is_read ASC, created_at DESC, id DESC 
                LIMIT 20;";

            var sw = Stopwatch.StartNew();
            using var reader = await notifCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
            sw.Stop();

            sw.Elapsed.TotalMilliseconds.Should().BeLessThan(50.0);
        }

        // 2. Follows relationship query
        using (var followCmd = connection.CreateCommand())
        {
            followCmd.CommandText = $@"
                SELECT following_id 
                FROM follows 
                WHERE follower_id = '{userId}' 
                ORDER BY created_at DESC 
                LIMIT 50;";

            var sw = Stopwatch.StartNew();
            using var reader = await followCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
            sw.Stop();

            sw.Elapsed.TotalMilliseconds.Should().BeLessThan(50.0);
        }
    }

    private static string BuildHomeFeedSql(Guid viewerId)
    {
        return $@"
            SELECT p.id, p.author_id, p.content, p.reply_count, p.media_count, p.like_count, p.bookmark_count, p.is_deleted, p.created_at, p.updated_at
            FROM posts AS p
            INNER JOIN users AS u ON p.author_id = u.id
            WHERE (p.is_deleted = FALSE)
              AND (p.is_hidden = FALSE)
              AND (u.is_suspended = FALSE)
              AND (
                p.author_id = '{viewerId}'
                OR p.author_id IN (
                    SELECT f.following_id
                    FROM follows AS f
                    WHERE f.follower_id = '{viewerId}'
                )
              )
              AND NOT EXISTS (
                SELECT 1
                FROM blocks AS b
                WHERE (b.blocker_id = '{viewerId}' AND b.blocked_id = p.author_id)
                   OR (b.blocker_id = p.author_id AND b.blocked_id = '{viewerId}')
              )
            ORDER BY p.created_at DESC, p.id DESC
            LIMIT 21;";
    }

    private static string BuildProfileTimelineSql(Guid authorId)
    {
        return $@"
            SELECT p.id, p.author_id, p.content, p.reply_count, p.media_count, p.like_count, p.bookmark_count, p.is_deleted, p.created_at, p.updated_at
            FROM posts AS p
            WHERE p.author_id = '{authorId}'
              AND NOT p.is_deleted
              AND NOT p.is_hidden
            ORDER BY p.created_at DESC, p.id DESC
            LIMIT 21;";
    }
}
