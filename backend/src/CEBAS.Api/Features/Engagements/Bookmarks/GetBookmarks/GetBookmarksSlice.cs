using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Engagements;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Engagements.Bookmarks.GetBookmarks;

public sealed record GetBookmarksQuery(
    Guid ActorUserId,
    string? Cursor,
    int Limit = 20
) : IRequest<CursorPagedResult<BookmarkedPostResponse>>;

public sealed class GetBookmarksQueryValidator : AbstractValidator<GetBookmarksQuery>
{
    public GetBookmarksQueryValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50).WithMessage("Limit must be between 1 and 50.");
    }
}

public sealed class GetBookmarksQueryHandler : IRequestHandler<GetBookmarksQuery, CursorPagedResult<BookmarkedPostResponse>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GetBookmarksQueryHandler> _logger;

    public GetBookmarksQueryHandler(
        ApplicationDbContext dbContext,
        ILogger<GetBookmarksQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CursorPagedResult<BookmarkedPostResponse>> Handle(
        GetBookmarksQuery request, CancellationToken cancellationToken)
    {
        var cursor = Cursor.Decode(request.Cursor);
        var pageSize = Math.Clamp(request.Limit, 1, 50);

        // Build query: user's bookmarks with post data, ordered by bookmark creation time DESC
        var query = _dbContext.PostBookmarks
            .AsNoTracking()
            .Where(b => b.UserId == request.ActorUserId)
            .Join(
                _dbContext.Posts.AsNoTracking().Where(p => !p.IsDeleted),
                b => b.PostId,
                p => p.Id,
                (b, p) => new { Bookmark = b, Post = p }
            );

        // Keyset cursor pagination
        if (cursor != null)
        {
            query = query.Where(x =>
                x.Bookmark.CreatedAt < cursor.CreatedAt ||
                (x.Bookmark.CreatedAt == cursor.CreatedAt && x.Bookmark.Id.CompareTo(cursor.Id) < 0));
        }

        var results = await query
            .OrderByDescending(x => x.Bookmark.CreatedAt)
            .ThenByDescending(x => x.Bookmark.Id)
            .Take(pageSize + 1)
            .Select(x => new
            {
                x.Bookmark.Id,
                x.Bookmark.CreatedAt,
                x.Bookmark.PostId,
                PostContent = x.Post.Content,
                PostAuthorId = x.Post.AuthorId,
                x.Post.ReplyCount,
                x.Post.MediaCount,
                x.Post.LikeCount,
                x.Post.BookmarkCount,
                x.Post.IsDeleted,
                PostCreatedAt = x.Post.CreatedAt,
                PostUpdatedAt = x.Post.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        // Load author info and media for posts
        var postIds = results.Select(r => r.PostId).Distinct().ToList();
        var authorIds = results.Select(r => r.PostAuthorId).Distinct().ToList();

        var authors = await _dbContext.Users
            .AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .Select(u => new PostAuthorDto(u.Id, u.Username, u.DisplayName, u.AvatarUrl, u.IsVerified))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var mediaByPost = await _dbContext.PostMedia
            .AsNoTracking()
            .Where(pm => postIds.Contains(pm.PostId))
            .OrderBy(pm => pm.Position)
            .Select(pm => new { pm.PostId, Dto = new PostMediaDto(pm.MediaId, $"/api/v1/media/{pm.MediaId}", pm.Media != null ? pm.Media.OriginalFileName : null, pm.Media != null ? pm.Media.MimeType : null, pm.Position) })
            .ToListAsync(cancellationToken);

        var mediaLookup = mediaByPost
            .GroupBy(m => m.PostId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Dto).ToList());

        // Check which posts the user has liked
        var likedPostIds = await _dbContext.PostLikes
            .AsNoTracking()
            .Where(l => l.UserId == request.ActorUserId && postIds.Contains(l.PostId))
            .Select(l => l.PostId)
            .ToListAsync(cancellationToken);
        var likedSet = new HashSet<Guid>(likedPostIds);

        var items = results.Select(r => new BookmarkedPostResponse(
            BookmarkId: r.Id,
            BookmarkedAt: r.CreatedAt,
            PostId: r.PostId,
            Content: r.PostContent,
            Author: authors.GetValueOrDefault(r.PostAuthorId) ?? new PostAuthorDto(r.PostAuthorId, "unknown", "Unknown", null, false),
            Media: mediaLookup.GetValueOrDefault(r.PostId) ?? [],
            ReplyCount: r.ReplyCount,
            MediaCount: r.MediaCount,
            LikeCount: r.LikeCount,
            BookmarkCount: r.BookmarkCount,
            Liked: likedSet.Contains(r.PostId),
            Bookmarked: true,
            IsDeleted: r.IsDeleted,
            CreatedAt: r.PostCreatedAt,
            UpdatedAt: r.PostUpdatedAt
        )).ToList();

        return CursorPagedResult<BookmarkedPostResponse>.Create(
            items.AsReadOnly(),
            pageSize,
            item => new Cursor(item.BookmarkedAt, item.BookmarkId)
        );
    }
}
