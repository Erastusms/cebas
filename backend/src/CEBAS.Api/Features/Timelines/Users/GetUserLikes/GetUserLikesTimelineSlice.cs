using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Timelines.Users.GetUserLikes;

public sealed record GetUserLikesTimelineQuery(
    string UserIdOrUsername,
    Guid? ViewerUserId,
    string? Cursor = null,
    int Limit = 20
) : IRequest<CursorPagedResult<PostResponse>>;

public sealed class GetUserLikesTimelineQueryValidator : AbstractValidator<GetUserLikesTimelineQuery>
{
    public GetUserLikesTimelineQueryValidator()
    {
        RuleFor(x => x.UserIdOrUsername)
            .NotEmpty().WithMessage("User ID or username is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50).WithMessage("Limit must be between 1 and 50.");

        RuleFor(x => x.Cursor)
            .Must(cursor =>
            {
                if (string.IsNullOrWhiteSpace(cursor)) return true;
                var valid = Cursor.TryDecode(cursor, out _, out _);
                if (!valid)
                {
                    TimelineMetrics.CursorInvalidCount.Add(1);
                }
                return valid;
            })
            .WithMessage("The provided pagination cursor is invalid, corrupted, or out of bounds.");
    }
}

public sealed class GetUserLikesTimelineQueryHandler : IRequestHandler<GetUserLikesTimelineQuery, CursorPagedResult<PostResponse>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly IAuthorProfileCache _authorCache;
    private readonly ILogger<GetUserLikesTimelineQueryHandler> _logger;

    public GetUserLikesTimelineQueryHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        IAuthorProfileCache authorCache,
        ILogger<GetUserLikesTimelineQueryHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _authorCache = authorCache;
        _logger = logger;
    }

    public async Task<CursorPagedResult<PostResponse>> Handle(GetUserLikesTimelineQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimelineMetrics.ProfileLikesRequestCount.Add(1);

        int pageSize = Math.Clamp(request.Limit, 1, 50);

        // 1. Resolve target user by GUID or username
        Domain.Entities.User? targetUser;
        if (Guid.TryParse(request.UserIdOrUsername, out var userId))
        {
            targetUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        }
        else
        {
            var normalized = IdentityNormalizers.NormalizeUsername(request.UserIdOrUsername);
            targetUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, cancellationToken);
        }

        if (targetUser == null)
        {
            _logger.LogInformation("user.likes: User '{UserIdOrUsername}' not found", request.UserIdOrUsername);
            throw new NotFoundException($"User '{request.UserIdOrUsername}' was not found.");
        }

        // 2. Server-side Block Isolation check
        if (request.ViewerUserId.HasValue && request.ViewerUserId.Value != targetUser.Id)
        {
            var isBlocked = await _blockIsolationService.IsBlockedBidirectionalAsync(
                request.ViewerUserId.Value,
                targetUser.Id,
                cancellationToken);

            if (isBlocked)
            {
                _logger.LogInformation("user.likes: Block isolation active between viewer {ViewerId} and user {UserId}",
                    request.ViewerUserId.Value, targetUser.Id);
                throw new NotFoundException($"User '{request.UserIdOrUsername}' was not found.");
            }
        }

        // 3. Base Query: User's likes joined with active posts
        var query = _dbContext.PostLikes
            .AsNoTracking()
            .Where(pl => pl.UserId == targetUser.Id)
            .Join(
                _dbContext.Posts.AsNoTracking().Where(p => !p.IsDeleted),
                pl => pl.PostId,
                p => p.Id,
                (pl, p) => new { Like = pl, Post = p }
            );

        // Dynamic block filtering: Exclude posts whose authors have blocking relationship with viewer
        if (request.ViewerUserId.HasValue)
        {
            var viewerId = request.ViewerUserId.Value;
            query = query.Where(x => !_dbContext.Blocks.Any(b =>
                (b.BlockerId == viewerId && b.BlockedId == x.Post.AuthorId) ||
                (b.BlockerId == x.Post.AuthorId && b.BlockedId == viewerId)));
        }

        // 4. Keyset cursor predicate based on Like relationship timestamp (created_at DESC, id DESC)
        Cursor.TryDecode(request.Cursor, out var cursor, out _);
        if (cursor != null)
        {
            query = query.Where(x =>
                x.Like.CreatedAt < cursor.CreatedAt ||
                (x.Like.CreatedAt == cursor.CreatedAt && x.Like.Id.CompareTo(cursor.Id) < 0));
        }

        var dbStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await query
            .OrderByDescending(x => x.Like.CreatedAt)
            .ThenByDescending(x => x.Like.Id)
            .Take(pageSize + 1)
            .Select(x => new
            {
                LikeId = x.Like.Id,
                LikeCreatedAt = x.Like.CreatedAt,
                PostId = x.Post.Id,
                AuthorId = x.Post.AuthorId,
                Content = x.Post.Content,
                ReplyCount = x.Post.ReplyCount,
                MediaCount = x.Post.MediaCount,
                LikeCount = x.Post.LikeCount,
                BookmarkCount = x.Post.BookmarkCount,
                IsDeleted = x.Post.IsDeleted,
                PostCreatedAt = x.Post.CreatedAt,
                PostUpdatedAt = x.Post.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        dbStopwatch.Stop();
        TimelineMetrics.DatabaseQueryDuration.Record(dbStopwatch.Elapsed.TotalMilliseconds);

        var postIds = results.Select(r => r.PostId).Distinct().ToList();
        var authorIds = results.Select(r => r.AuthorId).Distinct().ToList();

        // 5. Batch-load cached author profiles
        var authors = await _authorCache.GetAuthorsAsync(authorIds, cancellationToken);

        // 6. Batch-load media attachments
        var mediaList = postIds.Count > 0
            ? await _dbContext.PostMedia
                .AsNoTracking()
                .Where(pm => postIds.Contains(pm.PostId))
                .OrderBy(pm => pm.Position)
                .Select(pm => new
                {
                    pm.PostId,
                    pm.MediaId,
                    OriginalFileName = pm.Media != null ? pm.Media.OriginalFileName : null,
                    MimeType = pm.Media != null ? pm.Media.MimeType : null,
                    pm.Position
                })
                .ToListAsync(cancellationToken)
            : [];

        var mediaByPost = mediaList
            .GroupBy(m => m.PostId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(m => new PostMediaDto(
                    m.MediaId,
                    $"/api/v1/media/{m.MediaId}",
                    m.OriginalFileName,
                    m.MimeType,
                    m.Position
                )).ToList());

        // 7. Viewer-specific engagement state (resolved for the VIEWER, not target user)
        HashSet<Guid> viewerLikedPostIds = [];
        HashSet<Guid> viewerBookmarkedPostIds = [];

        if (request.ViewerUserId.HasValue && postIds.Count > 0)
        {
            var viewerId = request.ViewerUserId.Value;
            var liked = await _dbContext.PostLikes
                .AsNoTracking()
                .Where(l => l.UserId == viewerId && postIds.Contains(l.PostId))
                .Select(l => l.PostId)
                .ToListAsync(cancellationToken);
            viewerLikedPostIds = liked.ToHashSet();

            var bookmarked = await _dbContext.PostBookmarks
                .AsNoTracking()
                .Where(b => b.UserId == viewerId && postIds.Contains(b.PostId))
                .Select(b => b.PostId)
                .ToListAsync(cancellationToken);
            viewerBookmarkedPostIds = bookmarked.ToHashSet();
        }

        // 8. Map to PostResponse models
        var mappedList = results.Select(r =>
        {
            var authorDto = authors.GetValueOrDefault(r.AuthorId) ??
                new PostAuthorDto(r.AuthorId, "unknown", "Unknown", null, false);
            var mediaDtos = mediaByPost.GetValueOrDefault(r.PostId) ?? [];

            return new PostResponse(
                r.PostId,
                r.Content,
                authorDto,
                mediaDtos,
                r.ReplyCount,
                r.MediaCount,
                r.LikeCount,
                r.BookmarkCount,
                viewerLikedPostIds.Contains(r.PostId),
                viewerBookmarkedPostIds.Contains(r.PostId),
                r.IsDeleted,
                r.PostCreatedAt,
                r.PostUpdatedAt
            );
        }).ToList();

        bool hasNextPage = mappedList.Count > pageSize;
        var pagedItems = hasNextPage ? mappedList.Take(pageSize).ToList() : mappedList;
        string? nextCursor = null;
        if (hasNextPage && pagedItems.Count > 0)
        {
            var lastResult = results[pageSize - 1];
            nextCursor = new Cursor(lastResult.LikeCreatedAt, lastResult.LikeId).Encode();
        }

        var result = new CursorPagedResult<PostResponse>
        {
            Items = pagedItems.AsReadOnly(),
            NextCursor = nextCursor,
            HasNextPage = hasNextPage,
            PageSize = pageSize
        };

        stopwatch.Stop();
        TimelineMetrics.ProfileLikesLatency.Record(stopwatch.Elapsed.TotalMilliseconds);
        return result;
    }
}
