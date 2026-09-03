using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Posts.GetFeed;

public sealed record GetFeedQuery(
    Guid? ViewerUserId,
    string? Cursor = null,
    int Limit = 20
) : IRequest<CursorPagedResult<PostResponse>>;

public sealed class GetFeedQueryHandler : IRequestHandler<GetFeedQuery, CursorPagedResult<PostResponse>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<GetFeedQueryHandler> _logger;

    public GetFeedQueryHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<GetFeedQueryHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<CursorPagedResult<PostResponse>> Handle(GetFeedQuery request, CancellationToken cancellationToken)
    {
        int pageSize = Math.Clamp(request.Limit, 1, 50);

        IQueryable<Post> query = _dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.MediaAttachments)
                .ThenInclude(pm => pm.Media)
            .Where(p => !p.IsDeleted);

        // 1. Block isolation and personalized following timeline
        if (request.ViewerUserId.HasValue)
        {
            var viewerId = request.ViewerUserId.Value;

            var blockedUserIds = await _blockIsolationService.GetBidirectionalBlockedUserIdsAsync(viewerId, cancellationToken);
            if (blockedUserIds.Count > 0)
            {
                query = query.Where(p => !blockedUserIds.Contains(p.AuthorId));
            }

            var followingIds = await _dbContext.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == viewerId)
                .Select(f => f.FollowingId)
                .ToListAsync(cancellationToken);

            // If user follows people, show feed of followed users + self; if following none, fallback to discovery feed
            if (followingIds.Count > 0)
            {
                var targetAuthors = followingIds.Append(viewerId).ToHashSet();
                query = query.Where(p => targetAuthors.Contains(p.AuthorId));
            }
        }

        // 2. Keyset cursor pagination (CreatedAt DESC, Id DESC)
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var decoded = Cursor.Decode(request.Cursor);
            if (decoded != null)
            {
                query = query.Where(p =>
                    p.CreatedAt < decoded.CreatedAt ||
                    (p.CreatedAt == decoded.CreatedAt && p.Id.CompareTo(decoded.Id) < 0));
            }
        }

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var postIds = posts.Select(p => p.Id).ToList();
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

        var mappedList = posts.Select(p =>
        {
            var authorDto = new PostAuthorDto(
                p.Author!.Id,
                p.Author.Username,
                p.Author.DisplayName,
                p.Author.AvatarUrl,
                p.Author.IsVerified
            );

            var mediaDtos = p.MediaAttachments
                .OrderBy(pm => pm.Position)
                .Where(pm => pm.Media != null)
                .Select(pm => new PostMediaDto(
                    pm.MediaId,
                    $"/api/v1/media/{pm.MediaId}",
                    pm.Media!.OriginalFileName,
                    pm.Media.MimeType,
                    pm.Position
                ))
                .ToList();

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

        return CursorPagedResult<PostResponse>.Create(
            mappedList,
            pageSize,
            item => new Cursor(item.CreatedAt, item.Id)
        );
    }
}
