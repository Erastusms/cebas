using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Timelines.Home;

public sealed record GetHomeTimelineQuery(
    Guid ViewerUserId,
    string? Cursor = null,
    int Limit = 20
) : IRequest<CursorPagedResult<PostResponse>>;

public sealed class GetHomeTimelineQueryValidator : AbstractValidator<GetHomeTimelineQuery>
{
    public GetHomeTimelineQueryValidator()
    {
        RuleFor(x => x.ViewerUserId)
            .NotEmpty().WithMessage("Viewer user ID is required for home timeline feed.");

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

public sealed class GetHomeTimelineQueryHandler : IRequestHandler<GetHomeTimelineQuery, CursorPagedResult<PostResponse>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthorProfileCache _authorCache;
    private readonly ILogger<GetHomeTimelineQueryHandler> _logger;

    public GetHomeTimelineQueryHandler(
        ApplicationDbContext dbContext,
        IAuthorProfileCache authorCache,
        ILogger<GetHomeTimelineQueryHandler> logger)
    {
        _dbContext = dbContext;
        _authorCache = authorCache;
        _logger = logger;
    }

    public async Task<CursorPagedResult<PostResponse>> Handle(GetHomeTimelineQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimelineMetrics.HomeRequestCount.Add(1);

        try
        {
            var pageSize = Math.Clamp(request.Limit, 1, 50);
            var viewerId = request.ViewerUserId;

            // Strict cursor decoding
            Cursor.TryDecode(request.Cursor, out var cursor, out _);

            // 1. Relational Feed Query:
            // - Active non-deleted posts
            // - Posts authored by followed users OR viewer themselves
            // - Dynamic bidirectional block filtering evaluated server-side in DB
            var query = _dbContext.Posts
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Where(p => p.AuthorId == viewerId ||
                            _dbContext.Follows
                                .Where(f => f.FollowerId == viewerId)
                                .Select(f => f.FollowingId)
                                .Contains(p.AuthorId))
                .Where(p => !_dbContext.Blocks.Any(b =>
                    (b.BlockerId == viewerId && b.BlockedId == p.AuthorId) ||
                    (b.BlockerId == p.AuthorId && b.BlockedId == viewerId)));

            // 2. Keyset cursor predicate: (created_at, id) < (cursor.CreatedAt, cursor.Id)
            if (cursor != null)
            {
                query = query.Where(p =>
                    p.CreatedAt < cursor.CreatedAt ||
                    (p.CreatedAt == cursor.CreatedAt && p.Id.CompareTo(cursor.Id) < 0));
            }

            // 3. Deterministic order: created_at DESC, id DESC
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

            // 4. Batch-load cached author profiles
            var authors = await _authorCache.GetAuthorsAsync(authorIds, cancellationToken);

            // 5. Batch-load media attachments
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

            // 6. Viewer-specific engagement state (likes & bookmarks) in batch lookups
            HashSet<Guid> likedPostIds = [];
            HashSet<Guid> bookmarkedPostIds = [];

            if (postIds.Count > 0)
            {
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

            // 7. Map to PostResponse models
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
            TimelineMetrics.HomeLatency.Record(stopwatch.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            TimelineMetrics.HomeErrorCount.Add(1);
            _logger.LogError(ex, "Failed to retrieve home timeline for viewer {ViewerId}", request.ViewerUserId);
            throw;
        }
    }
}
