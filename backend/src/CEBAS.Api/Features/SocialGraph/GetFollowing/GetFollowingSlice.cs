using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.SocialGraph;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.SocialGraph.GetFollowing;

public sealed record GetFollowingQuery(
    Guid TargetUserId,
    Guid? CurrentUserId,
    string? CursorString,
    int Limit = 20
) : IRequest<CursorPagedResult<SocialUserDto>>;

public sealed class GetFollowingQueryValidator : AbstractValidator<GetFollowingQuery>
{
    public GetFollowingQueryValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Target user ID is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50).WithMessage("Page limit must be between 1 and 50.");
    }
}

public sealed class GetFollowingQueryHandler : IRequestHandler<GetFollowingQuery, CursorPagedResult<SocialUserDto>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<GetFollowingQueryHandler> _logger;

    public GetFollowingQueryHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<GetFollowingQueryHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<CursorPagedResult<SocialUserDto>> Handle(GetFollowingQuery request, CancellationToken cancellationToken)
    {
        // 1. Target user existence check
        var targetExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (!targetExists)
        {
            _logger.LogWarning("social_graph.query.failed: Target user {TargetUserId} not found", request.TargetUserId);
            throw new NotFoundException($"User with ID '{request.TargetUserId}' was not found.");
        }

        // 2. Check if current user is blocked bidirectionally with target user
        if (request.CurrentUserId.HasValue)
        {
            var isBlockedWithTarget = await _blockIsolationService.IsBlockedBidirectionalAsync(
                request.CurrentUserId.Value, request.TargetUserId, cancellationToken);

            if (isBlockedWithTarget)
            {
                return new CursorPagedResult<SocialUserDto>
                {
                    Items = [],
                    NextCursor = null,
                    HasNextPage = false,
                    PageSize = request.Limit
                };
            }
        }

        // 3. Base query: get users that target user is following (f.FollowerId == targetUserId)
        var query = _dbContext.Follows
            .AsNoTracking()
            .Where(f => f.FollowerId == request.TargetUserId);

        // 4. Server-side block isolation: exclude accounts blocked by or blocking current user
        if (request.CurrentUserId.HasValue)
        {
            var currentUserId = request.CurrentUserId.Value;
            query = query.Where(f =>
                !_dbContext.Blocks.Any(b => (b.BlockerId == currentUserId && b.BlockedId == f.FollowingId) ||
                                            (b.BlockerId == f.FollowingId && b.BlockedId == currentUserId)));
        }

        // 5. Keyset / Cursor Pagination (CreatedAt DESC, Id DESC)
        var cursor = Cursor.Decode(request.CursorString);
        if (cursor != null)
        {
            query = query.Where(f => f.CreatedAt < cursor.CreatedAt ||
                                    (f.CreatedAt == cursor.CreatedAt && f.Id < cursor.Id));
        }

        var followRecords = await query
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .Take(request.Limit + 1)
            .Select(f => new
            {
                FollowId = f.Id,
                FollowedAt = f.CreatedAt,
                User = f.Following!
            })
            .ToListAsync(cancellationToken);

        if (followRecords.Count == 0)
        {
            return new CursorPagedResult<SocialUserDto>
            {
                Items = [],
                NextCursor = null,
                HasNextPage = false,
                PageSize = request.Limit
            };
        }

        var userIds = followRecords.Select(r => r.User.Id).Distinct().ToList();

        // 6. Set-based relationship state retrieval for current user (Eliminates N+1 queries)
        HashSet<Guid> followingSet = new();
        HashSet<Guid> followedBySet = new();
        HashSet<Guid> blockedSet = new();

        if (request.CurrentUserId.HasValue)
        {
            var currentUserId = request.CurrentUserId.Value;

            followingSet = (await _dbContext.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentUserId && userIds.Contains(f.FollowingId))
                .Select(f => f.FollowingId)
                .ToListAsync(cancellationToken)).ToHashSet();

            followedBySet = (await _dbContext.Follows
                .AsNoTracking()
                .Where(f => f.FollowingId == currentUserId && userIds.Contains(f.FollowerId))
                .Select(f => f.FollowerId)
                .ToListAsync(cancellationToken)).ToHashSet();

            blockedSet = (await _dbContext.Blocks
                .AsNoTracking()
                .Where(b => b.BlockerId == currentUserId && userIds.Contains(b.BlockedId))
                .Select(b => b.BlockedId)
                .ToListAsync(cancellationToken)).ToHashSet();
        }

        var items = followRecords.Select(r => new SocialUserDto(
            r.User.Id,
            r.User.Username,
            r.User.DisplayName,
            r.User.Bio,
            r.User.AvatarUrl,
            r.User.IsVerified,
            r.FollowedAt,
            r.FollowId,
            followingSet.Contains(r.User.Id),
            followedBySet.Contains(r.User.Id),
            blockedSet.Contains(r.User.Id)
        )).ToList();

        return CursorPagedResult<SocialUserDto>.Create(items, request.Limit, item => new Cursor(item.FollowedAt, item.FollowId));
    }
}
