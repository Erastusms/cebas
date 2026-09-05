using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Hubs;

/// <summary>
/// Authenticated SignalR Hub providing real-time notification delivery and post engagement synchronization.
/// </summary>
[Authorize]
public class SocialHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<SocialHub> _logger;

    public SocialHub(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<SocialHub> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetAuthenticatedUserId();
        if (userId.HasValue)
        {
            var userGroup = $"user:{userId.Value}";
            await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);
            _logger.LogInformation("SocialHub: Client {ConnectionId} bound to private group {UserGroup}",
                Context.ConnectionId, userGroup);
        }
        else
        {
            _logger.LogWarning("SocialHub: Connected client {ConnectionId} has no valid NameIdentifier claim",
                Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetAuthenticatedUserId();
        if (userId.HasValue)
        {
            _logger.LogInformation("SocialHub: Client {ConnectionId} (User: {UserId}) disconnected",
                Context.ConnectionId, userId.Value);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribes the current connection to real-time events for a specific post (live like counters, replies).
    /// Enforces post existence and bidirectional block isolation.
    /// </summary>
    public async Task JoinPostGroup(Guid postId)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            throw new HubException("Unauthorized: Authentication required to subscribe to post updates.");
        }

        var post = await _dbContext.Posts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);

        if (post == null)
        {
            throw new HubException($"Post '{postId}' was not found or is deleted.");
        }

        // Server-side security boundary: Verify block isolation with post author
        var isBlocked = await _blockIsolationService.IsBlockedBidirectionalAsync(userId.Value, post.AuthorId);
        if (isBlocked)
        {
            _logger.LogWarning("SocialHub: Block isolation rejection for user {UserId} trying to join post {PostId}",
                userId.Value, postId);
            throw new HubException("Cannot subscribe to this post due to block or privacy restrictions.");
        }

        var postGroup = $"post:{postId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, postGroup);
        _logger.LogDebug("SocialHub: Client {ConnectionId} joined {PostGroup}", Context.ConnectionId, postGroup);
    }

    /// <summary>
    /// Unsubscribes the current connection from real-time events for a specific post.
    /// </summary>
    public async Task LeavePostGroup(Guid postId)
    {
        var postGroup = $"post:{postId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, postGroup);
        _logger.LogDebug("SocialHub: Client {ConnectionId} left {PostGroup}", Context.ConnectionId, postGroup);
    }

    private Guid? GetAuthenticatedUserId()
    {
        var claimValue = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
