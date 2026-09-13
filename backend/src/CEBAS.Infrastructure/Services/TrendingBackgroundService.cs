using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CEBAS.Application.Abstractions;
using CEBAS.Infrastructure.Configuration;

namespace CEBAS.Infrastructure.Services;

/// <summary>
/// Background hosted service that periodically pre-aggregates top trending hashtags,
/// refreshes the Redis and in-memory caches, and removes expired hourly buckets.
/// </summary>
public class TrendingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<TrendingOptions> _options;
    private readonly ILogger<TrendingBackgroundService> _logger;

    public TrendingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<TrendingOptions> options,
        ILogger<TrendingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(15, _options.Value.AggregationIntervalSeconds);
        _logger.LogInformation("TrendingBackgroundService started with aggregation interval {Interval}s", intervalSeconds);

        // Initial delay to allow backend startup and connection stabilization
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var trendingService = scope.ServiceProvider.GetRequiredService<ITrendingService>();

                // 1. Refresh trending aggregation cache
                await trendingService.PrecomputeAndCacheTrendsAsync(stoppingToken);

                // 2. Perform targeted cleanup of expired buckets
                await trendingService.CleanupExpiredBucketsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TrendingBackgroundService loop");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("TrendingBackgroundService stopped gracefully.");
    }
}
