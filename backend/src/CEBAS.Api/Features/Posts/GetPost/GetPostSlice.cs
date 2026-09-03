using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Posts.GetPost;

public sealed record GetPostQuery(
    Guid PostId,
    Guid? ViewerUserId
) : IRequest<PostResponse>;

public sealed class GetPostQueryHandler : IRequestHandler<GetPostQuery, PostResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<GetPostQueryHandler> _logger;

    public GetPostQueryHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<GetPostQueryHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<PostResponse> Handle(GetPostQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch post with eager-loaded author and media attachments
        var post = await _dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.MediaAttachments)
                .ThenInclude(pm => pm.Media)
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post == null || post.IsDeleted)
        {
            _logger.LogInformation("post.get: Post {PostId} not found or soft-deleted", request.PostId);
            throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
        }

        // 2. Server-side block isolation check
        if (request.ViewerUserId.HasValue)
        {
            var isBlocked = await _blockIsolationService.IsBlockedBidirectionalAsync(
                request.ViewerUserId.Value,
                post.AuthorId,
                cancellationToken);

            if (isBlocked)
            {
                _logger.LogInformation("post.get: Block isolation active between viewer {ViewerId} and author {AuthorId}",
                    request.ViewerUserId.Value, post.AuthorId);
                throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
            }
        }

        // 3. Check viewer engagement state (liked, bookmarked)
        bool isLiked = false;
        bool isBookmarked = false;

        if (request.ViewerUserId.HasValue)
        {
            var viewerId = request.ViewerUserId.Value;
            isLiked = await _dbContext.PostLikes
                .AsNoTracking()
                .AnyAsync(l => l.PostId == post.Id && l.UserId == viewerId, cancellationToken);

            isBookmarked = await _dbContext.PostBookmarks
                .AsNoTracking()
                .AnyAsync(b => b.PostId == post.Id && b.UserId == viewerId, cancellationToken);
        }

        // 4. Map into response DTO
        var authorDto = new PostAuthorDto(
            post.Author!.Id,
            post.Author.Username,
            post.Author.DisplayName,
            post.Author.AvatarUrl,
            post.Author.IsVerified
        );

        var mediaDtos = post.MediaAttachments
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
            post.Id,
            post.Content,
            authorDto,
            mediaDtos,
            post.ReplyCount,
            post.MediaCount,
            post.LikeCount,
            post.BookmarkCount,
            isLiked,
            isBookmarked,
            post.IsDeleted,
            post.CreatedAt,
            post.UpdatedAt
        );
    }
}
