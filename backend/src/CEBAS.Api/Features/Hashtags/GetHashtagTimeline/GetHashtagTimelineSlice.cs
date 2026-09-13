using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Hashtags.GetHashtagTimeline;

public sealed record GetHashtagTimelineQuery(
    string Tag,
    Guid? ViewerUserId,
    string? Cursor = null,
    int Limit = 20
) : IRequest<CursorPagedResult<PostResponse>>;

public sealed class GetHashtagTimelineQueryValidator : AbstractValidator<GetHashtagTimelineQuery>
{
    public GetHashtagTimelineQueryValidator()
    {
        RuleFor(x => x.Tag)
            .NotEmpty().WithMessage("Hashtag parameter is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50).WithMessage("Limit must be between 1 and 50.");

        RuleFor(x => x.Cursor)
            .Must(cursor =>
            {
                if (string.IsNullOrWhiteSpace(cursor)) return true;
                return Cursor.TryDecode(cursor, out _, out _);
            })
            .WithMessage("The provided pagination cursor is invalid, corrupted, or out of bounds.");
    }
}

public sealed class GetHashtagTimelineQueryHandler : IRequestHandler<GetHashtagTimelineQuery, CursorPagedResult<PostResponse>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHashtagParser _hashtagParser;
    private readonly IAuthorProfileCache _authorCache;
    private readonly ILogger<GetHashtagTimelineQueryHandler> _logger;

    public GetHashtagTimelineQueryHandler(
        ApplicationDbContext dbContext,
        IHashtagParser hashtagParser,
        IAuthorProfileCache authorCache,
        ILogger<GetHashtagTimelineQueryHandler> logger)
    {
        _dbContext = dbContext;
        _hashtagParser = hashtagParser;
        _authorCache = authorCache;
        _logger = logger;
    }

    public async Task<CursorPagedResult<PostResponse>> Handle(GetHashtagTimelineQuery request, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pageSize = Math.Clamp(request.Limit, 1, 50);
        var normalizedTag = _hashtagParser.Normalize(request.Tag);

        if (string.IsNullOrWhiteSpace(normalizedTag))
        {
            return CursorPagedResult<PostResponse>.Create(
                Array.Empty<PostResponse>(),
                pageSize,
                item => new Cursor(item.CreatedAt, item.Id)
            );
        }

        // Strict cursor decoding
        Cursor.TryDecode(request.Cursor, out var cursor, out _);

        // 1. Relational Feed Query:
        // - Posts tagged with requested normalized hashtag
        // - Active non-deleted, non-hidden posts from non-suspended authors
        // - Server-side dynamic bidirectional block filtering if viewer is authenticated
        var query = _dbContext.PostHashtags
            .AsNoTracking()
            .Where(ph => ph.Hashtag!.NormalizedName == normalizedTag)
            .Join(_dbContext.Posts.AsNoTracking(),
                  ph => ph.PostId,
                  p => p.Id,
                  (ph, p) => p)
            .Where(p => !p.IsDeleted && !p.IsHidden && !p.Author!.IsSuspended);

        if (request.ViewerUserId.HasValue)
        {
            var viewerId = request.ViewerUserId.Value;
            query = query.Where(p => !_dbContext.Blocks.Any(b =>
                (b.BlockerId == viewerId && b.BlockedId == p.AuthorId) ||
                (b.BlockerId == p.AuthorId && b.BlockedId == viewerId)));
        }

        // 2. Keyset cursor predicate: (created_at, id) < (cursor.CreatedAt, cursor.Id)
        if (cursor != null)
        {
            query = query.Where(p =>
                p.CreatedAt < cursor.CreatedAt ||
                (p.CreatedAt == cursor.CreatedAt && p.Id.CompareTo(cursor.Id) < 0));
        }

        // 3. Deterministic order: created_at DESC, id DESC
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

        var postIds = posts.Select(p => p.Id).ToList();
        var authorIds = posts.Select(p => p.AuthorId).Distinct().ToList();

        // 4. Batch load cached author profiles
        var authors = await _authorCache.GetAuthorsAsync(authorIds, cancellationToken);

        // 5. Batch load media attachments
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

        // 6. Viewer-specific engagement state (likes & bookmarks)
        HashSet<Guid> likedPostIds = [];
        HashSet<Guid> bookmarkedPostIds = [];

        if (request.ViewerUserId.HasValue && postIds.Count > 0)
        {
            var viewerId = request.ViewerUserId.Value;
            likedPostIds = (await _dbContext.PostLikes
                .AsNoTracking()
                .Where(l => l.UserId == viewerId && postIds.Contains(l.PostId))
                .Select(l => l.PostId)
                .ToListAsync(cancellationToken)).ToHashSet();

            bookmarkedPostIds = (await _dbContext.PostBookmarks
                .AsNoTracking()
                .Where(b => b.UserId == viewerId && postIds.Contains(b.PostId))
                .Select(b => b.PostId)
                .ToListAsync(cancellationToken)).ToHashSet();
        }

        // 7. Map to PostResponse and construct Keyset pagination result
        var postResponses = posts.Select(p =>
        {
            authors.TryGetValue(p.AuthorId, out var author);
            mediaByPost.TryGetValue(p.Id, out var media);

            var authorDto = author ?? new PostAuthorDto(
                p.AuthorId,
                "deleted",
                "Deleted User",
                null,
                false
            );

            return new PostResponse(
                p.Id,
                p.Content,
                authorDto,
                media ?? [],
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
            postResponses,
            pageSize,
            item => new Cursor(item.CreatedAt, item.Id)
        );

        sw.Stop();
        TrendingMetrics.TagTimelineDuration.Record(sw.Elapsed.TotalMilliseconds);

        return result;
    }
}
