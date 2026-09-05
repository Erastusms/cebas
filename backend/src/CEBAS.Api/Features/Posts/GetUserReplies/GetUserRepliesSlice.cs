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

namespace CEBAS.Api.Features.Posts.GetUserReplies;

public sealed record GetUserRepliesQuery(
    string Username,
    Guid? ViewerUserId,
    string? Cursor = null,
    int Limit = 20
) : IRequest<CursorPagedResult<UserReplyResponse>>;

public sealed class GetUserRepliesQueryHandler : IRequestHandler<GetUserRepliesQuery, CursorPagedResult<UserReplyResponse>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<GetUserRepliesQueryHandler> _logger;

    public GetUserRepliesQueryHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<GetUserRepliesQueryHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<CursorPagedResult<UserReplyResponse>> Handle(GetUserRepliesQuery request, CancellationToken cancellationToken)
    {
        int pageSize = Math.Clamp(request.Limit, 1, 50);
        var normalized = IdentityNormalizers.NormalizeUsername(request.Username);

        // 1. Target user existence
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, cancellationToken);

        if (user == null || user.IsSuspended)
        {
            _logger.LogInformation("user.replies: User '@{Username}' not found or suspended", request.Username);
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
                _logger.LogInformation("user.replies: Block isolation active between viewer {ViewerId} and user {UserId}",
                    request.ViewerUserId.Value, user.Id);
                throw new NotFoundException($"User '@{request.Username}' was not found.");
            }
        }

        // 3. Query replies authored by this target user
        IQueryable<PostReply> query = _dbContext.PostReplies
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Post)
                .ThenInclude(p => p!.Author)
            .Include(r => r.ParentReply)
                .ThenInclude(pr => pr!.Author)
            .Where(r => r.AuthorId == user.Id && !r.IsDeleted && r.Post != null && !r.Post.IsDeleted && !r.Post.IsHidden);

        // 4. Keyset cursor pagination
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var decoded = Cursor.Decode(request.Cursor);
            if (decoded != null)
            {
                query = query.Where(r =>
                    r.CreatedAt < decoded.CreatedAt ||
                    (r.CreatedAt == decoded.CreatedAt && r.Id.CompareTo(decoded.Id) < 0));
            }
        }

        var replies = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var mappedList = replies.Select(r =>
        {
            var authorDto = new ReplyAuthorDto(
                r.Author!.Id,
                r.Author.Username,
                r.Author.DisplayName,
                r.Author.AvatarUrl,
                r.Author.IsVerified
            );

            // Determine recipient username (parent reply author or parent post author)
            string? replyingToUsername = (r.ParentReply?.Author?.IsSuspended == true)
                ? "[suspended]"
                : r.ParentReply?.Author?.Username ??
                  ((r.Post?.Author?.IsSuspended == true) ? "[suspended]" : r.Post?.Author?.Username);

            string? parentPostContent = (r.Post?.Author?.IsSuspended == true)
                ? "[Post is deleted]"
                : r.Post?.Content;

            return new UserReplyResponse(
                r.Id,
                r.PostId,
                r.ParentReplyId,
                r.Content,
                authorDto,
                replyingToUsername,
                parentPostContent,
                r.IsDeleted,
                r.CreatedAt,
                r.UpdatedAt
            );
        }).ToList();

        return CursorPagedResult<UserReplyResponse>.Create(
            mappedList,
            pageSize,
            item => new Cursor(item.CreatedAt, item.Id)
        );
    }
}
