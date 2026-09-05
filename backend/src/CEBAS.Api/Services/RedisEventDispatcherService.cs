using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using CEBAS.Api.Hubs;
using CEBAS.Application.Contracts.Events;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Services;

/// <summary>
/// Background service that subscribes to the Redis Pub/Sub events channel ("cebas:events")
/// and dispatches events to connected SignalR clients through the SocialHub.
/// </summary>
public class RedisEventDispatcherService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<SocialHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RedisEventDispatcherService> _logger;
    private const string RedisChannelName = "cebas:events";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisEventDispatcherService(
        IConnectionMultiplexer redis,
        IHubContext<SocialHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<RedisEventDispatcherService> logger)
    {
        _redis = redis;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RedisEventDispatcherService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_redis.IsConnected)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                var subscriber = _redis.GetSubscriber();
                await subscriber.SubscribeAsync(RedisChannel.Literal(RedisChannelName), async (channel, message) =>
                {
                    if (message.IsNullOrEmpty) return;

                    try
                    {
                        await DispatchMessageAsync(message.ToString(), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error dispatching Redis event to SignalR");
                    }
                });

                _logger.LogInformation("Successfully subscribed to Redis channel {Channel}", RedisChannelName);

                // Wait until cancellation or disconnection
                while (!stoppingToken.IsCancellationRequested && _redis.IsConnected)
                {
                    await Task.Delay(2000, stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Redis connection lost. Will attempt to resubscribe once reconnected.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Redis Pub/Sub subscription not yet available. Retrying in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public async Task DispatchMessageAsync(string messageJson, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(messageJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("eventType", out var eventTypeProp))
        {
            _logger.LogWarning("Discarding Redis message without eventType: {Message}", messageJson);
            return;
        }

        var eventType = eventTypeProp.GetString();
        _logger.LogDebug("Received event {EventType} from Redis Pub/Sub", eventType);

        switch (eventType)
        {
            case "NOTIFICATION_CREATED":
                if (root.TryGetProperty("recipientId", out var recipientIdProp) &&
                    recipientIdProp.TryGetGuid(out var recipientId))
                {
                    var userGroup = $"user:{recipientId}";
                    await _hubContext.Clients.Group(userGroup).SendAsync("NotificationReceived", messageJson, cancellationToken);
                    _logger.LogDebug("Dispatched NOTIFICATION_CREATED to SignalR group {UserGroup}", userGroup);
                }
                break;

            case "POST_LIKED":
                if (root.TryGetProperty("aggregateId", out var postLikedIdProp) &&
                    postLikedIdProp.TryGetGuid(out var postLikedId))
                {
                    var postGroup = $"post:{postLikedId}";
                    await _hubContext.Clients.Group(postGroup).SendAsync("PostLiked", messageJson, cancellationToken);
                    _logger.LogDebug("Dispatched POST_LIKED to SignalR group {PostGroup}", postGroup);
                }
                break;

            case "POST_UNLIKED":
                if (root.TryGetProperty("aggregateId", out var postUnlikedIdProp) &&
                    postUnlikedIdProp.TryGetGuid(out var postUnlikedId))
                {
                    var postGroup = $"post:{postUnlikedId}";
                    await _hubContext.Clients.Group(postGroup).SendAsync("PostUnliked", messageJson, cancellationToken);
                    _logger.LogDebug("Dispatched POST_UNLIKED to SignalR group {PostGroup}", postGroup);
                }
                break;

            case "REPLY_CREATED":
                if (root.TryGetProperty("aggregateId", out var replyPostIdProp) &&
                    replyPostIdProp.TryGetGuid(out var replyPostId))
                {
                    var postGroup = $"post:{replyPostId}";
                    await _hubContext.Clients.Group(postGroup).SendAsync("ReplyCreated", messageJson, cancellationToken);
                    _logger.LogDebug("Dispatched REPLY_CREATED to SignalR group {PostGroup}", postGroup);
                }
                break;

            case "POST_CREATED":
                if (root.TryGetProperty("actorId", out var authorIdProp) &&
                    authorIdProp.TryGetGuid(out var authorId))
                {
                    await DispatchPostCreatedToFollowersAsync(authorId, messageJson, cancellationToken);
                }
                break;

            default:
                _logger.LogDebug("Unhandled event type {EventType} in RedisEventDispatcherService", eventType);
                break;
        }
    }

    private async Task DispatchPostCreatedToFollowersAsync(Guid authorId, string messageJson, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Query followers of author excluding users who have blocked or are blocked by author
        var followers = await dbContext.Follows
            .AsNoTracking()
            .Where(f => f.FollowingId == authorId)
            .Select(f => f.FollowerId)
            .ToListAsync(cancellationToken);

        if (followers.Count == 0) return;

        // Block isolation filtering
        var blockedUserIds = await dbContext.Blocks
            .AsNoTracking()
            .Where(b => b.BlockerId == authorId || b.BlockedId == authorId)
            .Select(b => b.BlockerId == authorId ? b.BlockedId : b.BlockerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var eligibleFollowers = followers.Except(blockedUserIds).ToList();

        foreach (var followerId in eligibleFollowers)
        {
            var userGroup = $"user:{followerId}";
            await _hubContext.Clients.Group(userGroup).SendAsync("PostCreated", messageJson, cancellationToken);
        }

        _logger.LogDebug("Dispatched POST_CREATED to {Count} eligible followers for author {AuthorId}",
            eligibleFollowers.Count, authorId);
    }
}
