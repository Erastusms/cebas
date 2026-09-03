using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Domain.Common;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Posts.GetUserPosts;

public sealed record GetUserPostsQuery(
    string Username,
    Guid? ViewerUserId,
    string? Filter = "posts",
    string? Cursor = null,
    int Limit = 20
) : IRequest<CursorPagedResult<PostResponse>>;

public sealed class GetUserPostsQueryHandler : IRequestHandler<GetUserPostsQuery, CursorPagedResult<PostResponse>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<GetUserPostsQueryHandler> _logger;

    public GetUserPostsQueryHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<GetUserPostsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<CursorPagedResult<PostResponse>> Handle(GetUserPostsQuery request, CancellationToken cancellationToken)
    {
        int pageSize = Math.Clamp(request.Limit, 1, 50);
        var normalized = IdentityNormalizers.NormalizeUsername(request.Username);

        // 1. Target user existence
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, cancellationToken);

        if (user == null)
        {
            _logger.LogInformation("user.posts: User '@{Username}' not found", request.Username);
            throw new NotFoundException($"User '@{request.Username}' was not found.");
        }

        // 2. Block isolation check
        if (request.ViewerUserId.HasValue && request.ViewerUserId.Value != user.Id)
        {
            var isBlocked = await _blockIsolationService.IsBlockedBidirectionalAsync(
                request.ViewerUserId.Value,
                user.Id,
                cancellationToken);

            if (isBlocked)
            {
                _logger.LogInformation("user.posts: Block isolation active between viewer {ViewerId} and user {UserId}",
                    request.ViewerUserId.Value, user.Id);
                throw new NotFoundException($"User '@{request.Username}' was not found.");
            }
        }

        var filter = request.Filter?.Trim().ToLowerInvariant() ?? "posts";

        if (filter == "likes")
        {
            var likeQuery = _dbContext.PostLikes
                .AsNoTracking()
                .Where(pl => pl.UserId == user.Id)
                .Join(
                    _dbContext.Posts
                        .AsNoTracking()
                        .Include(p => p.Author)
                        .Include(p => p.MediaAttachments)
                            .ThenInclude(pm => pm.Media)
                        .Where(p => !p.IsDeleted),
                    pl => pl.PostId,
                    p => p.Id,
                    (pl, p) => new { Like = pl, Post = p }
                );

            if (request.ViewerUserId.HasValue)
            {
                var blockedUserIds = await _blockIsolationService.GetBidirectionalBlockedUserIdsAsync(request.ViewerUserId.Value, cancellationToken);
                if (blockedUserIds.Count > 0)
                {
                    likeQuery = likeQuery.Where(x => !blockedUserIds.Contains(x.Post.AuthorId));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var decoded = Cursor.Decode(request.Cursor);
                if (decoded != null)
                {
                    likeQuery = likeQuery.Where(x =>
                        x.Like.CreatedAt < decoded.CreatedAt ||
                        (x.Like.CreatedAt == decoded.CreatedAt && x.Like.Id.CompareTo(decoded.Id) < 0));
                }
            }

            var results = await likeQuery
                .OrderByDescending(x => x.Like.CreatedAt)
                .ThenByDescending(x => x.Like.Id)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            var postIds = results.Select(r => r.Post.Id).ToList();
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

            var mappedList = results.Select(r =>
            {
                var p = r.Post;
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

            bool hasNextPage = mappedList.Count > pageSize;
            var pagedItems = hasNextPage ? mappedList.Take(pageSize).ToList() : mappedList;
            string? nextCursor = null;
            if (hasNextPage && pagedItems.Count > 0)
            {
                var lastLike = results[pageSize - 1].Like;
                nextCursor = new Cursor(lastLike.CreatedAt, lastLike.Id).Encode();
            }

            return new CursorPagedResult<PostResponse>
            {
                Items = pagedItems.AsReadOnly(),
                NextCursor = nextCursor,
                HasNextPage = hasNextPage,
                PageSize = pageSize
            };
        }

        if (filter == "bookmarks")
        {
            // Bookmarks are private: only the authenticated owner can view their bookmarks
            if (!request.ViewerUserId.HasValue || request.ViewerUserId.Value != user.Id)
            {
                return new CursorPagedResult<PostResponse>
                {
                    Items = Array.Empty<PostResponse>(),
                    NextCursor = null,
                    HasNextPage = false,
                    PageSize = pageSize
                };
            }

            var bookmarkQuery = _dbContext.PostBookmarks
                .AsNoTracking()
                .Where(pb => pb.UserId == user.Id)
                .Join(
                    _dbContext.Posts
                        .AsNoTracking()
                        .Include(p => p.Author)
                        .Include(p => p.MediaAttachments)
                            .ThenInclude(pm => pm.Media)
                        .Where(p => !p.IsDeleted),
                    pb => pb.PostId,
                    p => p.Id,
                    (pb, p) => new { Bookmark = pb, Post = p }
                );

            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var decoded = Cursor.Decode(request.Cursor);
                if (decoded != null)
                {
                    bookmarkQuery = bookmarkQuery.Where(x =>
                        x.Bookmark.CreatedAt < decoded.CreatedAt ||
                        (x.Bookmark.CreatedAt == decoded.CreatedAt && x.Bookmark.Id.CompareTo(decoded.Id) < 0));
                }
            }

            var results = await bookmarkQuery
                .OrderByDescending(x => x.Bookmark.CreatedAt)
                .ThenByDescending(x => x.Bookmark.Id)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            var postIds = results.Select(r => r.Post.Id).ToList();
            HashSet<Guid> likedPostIds = [];

            if (postIds.Count > 0)
            {
                var liked = await _dbContext.PostLikes
                    .AsNoTracking()
                    .Where(l => l.UserId == user.Id && postIds.Contains(l.PostId))
                    .Select(l => l.PostId)
                    .ToListAsync(cancellationToken);
                likedPostIds = liked.ToHashSet();
            }

            var mappedList = results.Select(r =>
            {
                var p = r.Post;
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
                    true,
                    p.IsDeleted,
                    p.CreatedAt,
                    p.UpdatedAt
                );
            }).ToList();

            bool hasNextPage = mappedList.Count > pageSize;
            var pagedItems = hasNextPage ? mappedList.Take(pageSize).ToList() : mappedList;
            string? nextCursor = null;
            if (hasNextPage && pagedItems.Count > 0)
            {
                var lastBookmark = results[pageSize - 1].Bookmark;
                nextCursor = new Cursor(lastBookmark.CreatedAt, lastBookmark.Id).Encode();
            }

            return new CursorPagedResult<PostResponse>
            {
                Items = pagedItems.AsReadOnly(),
                NextCursor = nextCursor,
                HasNextPage = hasNextPage,
                PageSize = pageSize
            };
        }

        // 3. Filter strategy for author posts and media
        IQueryable<Post> query = _dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.MediaAttachments)
                .ThenInclude(pm => pm.Media)
            .Where(p => p.AuthorId == user.Id && !p.IsDeleted);

        if (filter == "media")
        {
            query = query.Where(p => p.MediaCount > 0);
        }

        // 4. Keyset cursor pagination
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

        var authorPostIds = posts.Select(p => p.Id).ToList();
        HashSet<Guid> authorLikedPostIds = [];
        HashSet<Guid> authorBookmarkedPostIds = [];

        if (request.ViewerUserId.HasValue && authorPostIds.Count > 0)
        {
            var viewerId = request.ViewerUserId.Value;
            var liked = await _dbContext.PostLikes
                .AsNoTracking()
                .Where(l => l.UserId == viewerId && authorPostIds.Contains(l.PostId))
                .Select(l => l.PostId)
                .ToListAsync(cancellationToken);
            authorLikedPostIds = liked.ToHashSet();

            var bookmarked = await _dbContext.PostBookmarks
                .AsNoTracking()
                .Where(b => b.UserId == viewerId && authorPostIds.Contains(b.PostId))
                .Select(b => b.PostId)
                .ToListAsync(cancellationToken);
            authorBookmarkedPostIds = bookmarked.ToHashSet();
        }

        var mappedPostsList = posts.Select(p =>
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
                authorLikedPostIds.Contains(p.Id),
                authorBookmarkedPostIds.Contains(p.Id),
                p.IsDeleted,
                p.CreatedAt,
                p.UpdatedAt
            );
        }).ToList();

        return CursorPagedResult<PostResponse>.Create(
            mappedPostsList,
            pageSize,
            item => new Cursor(item.CreatedAt, item.Id)
        );
    }
}
