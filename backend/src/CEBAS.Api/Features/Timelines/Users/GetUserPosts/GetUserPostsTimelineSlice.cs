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

namespace CEBAS.Api.Features.Timelines.Users.GetUserPosts;

public sealed record GetUserPostsTimelineQuery(
    string UserIdOrUsername,
    Guid? ViewerUserId,
    string? Filter = "posts",
    string? Cursor = null,
    int Limit = 20
) : IRequest<CursorPagedResult<PostResponse>>;

public sealed class GetUserPostsTimelineQueryValidator : AbstractValidator<GetUserPostsTimelineQuery>
{
    public GetUserPostsTimelineQueryValidator()
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

public sealed class GetUserPostsTimelineQueryHandler : IRequestHandler<GetUserPostsTimelineQuery, CursorPagedResult<PostResponse>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly IAuthorProfileCache _authorCache;
    private readonly ILogger<GetUserPostsTimelineQueryHandler> _logger;

    public GetUserPostsTimelineQueryHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        IAuthorProfileCache authorCache,
        ILogger<GetUserPostsTimelineQueryHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _authorCache = authorCache;
        _logger = logger;
    }

    public async Task<CursorPagedResult<PostResponse>> Handle(GetUserPostsTimelineQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimelineMetrics.ProfilePostsRequestCount.Add(1);

        int pageSize = Math.Clamp(request.Limit, 1, 50);

        // 1. Resolve Target User by GUID or username
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
            _logger.LogInformation("user.posts: User '{UserIdOrUsername}' not found", request.UserIdOrUsername);
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
                _logger.LogInformation("user.posts: Block isolation active between viewer {ViewerId} and user {UserId}",
                    request.ViewerUserId.Value, targetUser.Id);
                throw new NotFoundException($"User '{request.UserIdOrUsername}' was not found.");
            }
        }

        // 3. Build Base Keyset Query
        var filter = request.Filter?.Trim().ToLowerInvariant() ?? "posts";
        IQueryable<Domain.Entities.Post> query;

        if (filter == "bookmarks")
        {
            // Bookmarks are private to the user
            if (!request.ViewerUserId.HasValue || request.ViewerUserId.Value != targetUser.Id)
            {
                return CursorPagedResult<PostResponse>.Create([], pageSize, _ => new Cursor(DateTimeOffset.UtcNow, Guid.Empty));
            }

            var userBookmarkPostIds = await _dbContext.PostBookmarks
                .AsNoTracking()
                .Where(b => b.UserId == targetUser.Id)
                .Select(b => b.PostId)
                .ToListAsync(cancellationToken);

            query = _dbContext.Posts
                .AsNoTracking()
                .Where(p => userBookmarkPostIds.Contains(p.Id) && !p.IsDeleted);
        }
        else
        {
            query = _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.AuthorId == targetUser.Id && !p.IsDeleted);

            if (filter == "media")
            {
                query = query.Where(p => p.MediaCount > 0);
            }
        }

        // 4. Keyset cursor predicate
        Cursor.TryDecode(request.Cursor, out var cursor, out _);
        if (cursor != null)
        {
            query = query.Where(p =>
                p.CreatedAt < cursor.CreatedAt ||
                (p.CreatedAt == cursor.CreatedAt && p.Id.CompareTo(cursor.Id) < 0));
        }

        var dbStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(pageSize + 1)
            .Select(p => new
            {
                p.Id,
                p.AuthorId,
                p.Content,
                p.ReplyCount,
                p.MediaCount,
                p.LikeCount,
                p.BookmarkCount,
                p.IsDeleted,
                p.CreatedAt,
                p.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        dbStopwatch.Stop();
        TimelineMetrics.DatabaseQueryDuration.Record(dbStopwatch.Elapsed.TotalMilliseconds);

        var postIds = posts.Select(p => p.Id).ToList();
        var authorIds = posts.Select(p => p.AuthorId).Distinct().ToList();

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

        // 7. Viewer-specific engagement state (likes & bookmarks)
        HashSet<Guid> likedPostIds = [];
        HashSet<Guid> bookmarkedPostIds = [];

        if (request.ViewerUserId.HasValue && postIds.Count > 0)
        {
            var viewerId = request.ViewerUserId.Value;
            var liked = await _dbContext.PostLikes
                .AsNoTracking()
                .Where(l => l.UserId == viewerId && postIds.Contains(l.PostId))
                .Select(l => l.PostId)
                .ToListAsync(cancellationToken);
            likedPostIds = liked.ToHashSet();

            var bookmarked = await _dbContext.PostBookmarks
                .AsNoTracking()
                .Where(b => b.UserId == viewerId && postIds.Contains(b.PostId))
                .Select(b => b.PostId)
                .ToListAsync(cancellationToken);
            bookmarkedPostIds = bookmarked.ToHashSet();
        }

        // 8. Map to PostResponse models
        var mappedList = posts.Select(p =>
        {
            var authorDto = authors.GetValueOrDefault(p.AuthorId) ??
                new PostAuthorDto(p.AuthorId, "unknown", "Unknown", null, false);
            var mediaDtos = mediaByPost.GetValueOrDefault(p.Id) ?? [];

            return new PostResponse(
                p.Id,
                p.Content,
                authorDto,
                mediaDtos,
                p.ReplyCount,
                p.MediaCount,
                p.LikeCount,
                p.BookmarkCount,
                likedPostIds.Contains(p.Id),
                bookmarkedPostIds.Contains(p.Id),
                p.IsDeleted,
                p.CreatedAt,
                p.UpdatedAt
            );
        }).ToList();

        var result = CursorPagedResult<PostResponse>.Create(
            mappedList,
            pageSize,
            item => new Cursor(item.CreatedAt, item.Id)
        );

        stopwatch.Stop();
        TimelineMetrics.ProfilePostsLatency.Record(stopwatch.Elapsed.TotalMilliseconds);
        return result;
    }
}
