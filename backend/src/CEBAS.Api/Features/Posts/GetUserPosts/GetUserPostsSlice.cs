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

        // 3. Filter strategy
        IQueryable<Post> query = _dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.MediaAttachments)
                .ThenInclude(pm => pm.Media)
            .Where(p => p.AuthorId == user.Id && !p.IsDeleted);

        var filter = request.Filter?.Trim().ToLowerInvariant() ?? "posts";
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
