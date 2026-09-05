using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Infrastructure.Services;

/// <summary>
/// Managed background worker that continuously claims and dispatches durable outbox events to Redis Pub/Sub.
/// Employs PostgreSQL row-level locking (FOR UPDATE SKIP LOCKED) to allow concurrent worker scaling without duplicate processing.
/// </summary>
public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<OutboxProcessorService> _logger;
    private const string RedisChannelName = "cebas:events";

    public OutboxProcessorService(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorService started with poll interval {PollIntervalMs}ms, batch size {BatchSize}",
            _options.Value.PollIntervalMs, _options.Value.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processedCount = await ProcessBatchAsync(stoppingToken);

                // If no events were processed, delay for the configured polling interval
                if (processedCount == 0)
                {
                    await Task.Delay(_options.Value.PollIntervalMs, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in OutboxProcessorService loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxProcessorService stopped gracefully.");
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var swTotal = Stopwatch.StartNew();
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        int count = 0;

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTimeOffset.UtcNow;
                var lockTimeoutCutoff = now.AddSeconds(-_options.Value.ProcessingLockTimeoutSeconds);

                // 1. Claim pending/stale processing rows using PostgreSQL row-level SKIP LOCKED
                var claimSql = $@"
                    SELECT id FROM outbox_events
                    WHERE (status = 'PENDING' OR (status = 'PROCESSING' AND processed_at < @p0))
                      AND next_attempt_at <= @p1
                    ORDER BY created_at ASC
                    LIMIT {_options.Value.BatchSize}
                    FOR UPDATE SKIP LOCKED";

                var claimedIds = await dbContext.Database
                    .SqlQueryRaw<Guid>(claimSql, lockTimeoutCutoff, now)
                    .ToListAsync(cancellationToken);

                if (claimedIds.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                OutboxMetrics.EventsPolledCount.Add(claimedIds.Count);

                var events = await dbContext.OutboxEvents
                    .Where(e => claimedIds.Contains(e.Id))
                    .ToListAsync(cancellationToken);

                foreach (var evt in events)
                {
                    evt.MarkProcessing(now);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // 2. Publish each claimed event to Redis Pub/Sub independently
                count = events.Count;
                ISubscriber? subscriber = null;
                try
                {
                    if (_redis.IsConnected)
                    {
                        subscriber = _redis.GetSubscriber();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get Redis subscriber for outbox publishing");
                }

                foreach (var evt in events)
                {
                    await DispatchEventAsync(evt, subscriber, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed during outbox batch transaction claim");
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        swTotal.Stop();
        if (count > 0)
        {
            OutboxMetrics.BatchProcessingDuration.Record(swTotal.Elapsed.TotalMilliseconds);
        }

        return count;
    }

    private async Task DispatchEventAsync(OutboxEvent evt, ISubscriber? subscriber, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var reloaded = await dbContext.OutboxEvents.FirstOrDefaultAsync(e => e.Id == evt.Id, cancellationToken);
        if (reloaded == null) return;

        var publishSw = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow;

        try
        {
            if (subscriber == null || !_redis.IsConnected)
            {
                throw new InvalidOperationException("Redis connection is not available for publishing");
            }

            await subscriber.PublishAsync(
                RedisChannel.Literal(RedisChannelName),
                reloaded.Payload,
                CommandFlags.FireAndForget
            );

            publishSw.Stop();
            OutboxMetrics.PublishLatency.Record(publishSw.Elapsed.TotalMilliseconds);
            OutboxMetrics.EventsPublishedCount.Add(1);

            reloaded.MarkPublished(now);
            _logger.LogDebug("Outbox event {EventId} ({EventType}) published successfully to {Channel}",
                reloaded.Id, reloaded.EventType, RedisChannelName);
        }
        catch (Exception ex)
        {
            publishSw.Stop();
            var errorMessage = ex.Message;
            var attempt = reloaded.AttemptCount;

            if (attempt >= reloaded.MaxRetries)
            {
                reloaded.MarkFailed(errorMessage, null, now);
                OutboxMetrics.EventsFailedCount.Add(1);
                _logger.LogError(ex, "Outbox event {EventId} permanently failed after {AttemptCount} attempts: {Message}",
                    reloaded.Id, attempt, errorMessage);
            }
            else
            {
                // Exponential backoff: 2^attempt seconds (min 1s, max 300s)
                var backoffSeconds = Math.Min(300, Math.Pow(2, attempt));
                var backoff = TimeSpan.FromSeconds(backoffSeconds);
                reloaded.MarkFailed(errorMessage, backoff, now);
                OutboxMetrics.EventsRetriedCount.Add(1);
                _logger.LogWarning("Outbox event {EventId} failed on attempt {AttemptCount}. Will retry in {BackoffSeconds}s: {Message}",
                    reloaded.Id, attempt, backoffSeconds, errorMessage);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
