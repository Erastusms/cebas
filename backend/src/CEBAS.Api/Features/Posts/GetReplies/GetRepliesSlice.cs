using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Posts.GetReplies;

public sealed record GetRepliesQuery(
    Guid PostId,
    Guid? ViewerUserId,
    string? Cursor = null,
    int Limit = 50
) : IRequest<HierarchicalRepliesResult>;

public sealed class GetRepliesQueryHandler : IRequestHandler<GetRepliesQuery, HierarchicalRepliesResult>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<GetRepliesQueryHandler> _logger;

    public GetRepliesQueryHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<GetRepliesQueryHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<HierarchicalRepliesResult> Handle(GetRepliesQuery request, CancellationToken cancellationToken)
    {
        int pageSize = Math.Clamp(request.Limit, 1, 100);

        // 1. Post existence check
        var post = await _dbContext.Posts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post == null || post.IsDeleted || post.IsHidden)
        {
            _logger.LogInformation("reply.get: Post {PostId} not found, soft-deleted, or hidden", request.PostId);
            throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
        }

        // 2. Block isolation check on post author
        HashSet<Guid> blockedUserIds = new();
        if (request.ViewerUserId.HasValue)
        {
            var isPostAuthorBlocked = await _blockIsolationService.IsBlockedBidirectionalAsync(
                request.ViewerUserId.Value,
                post.AuthorId,
                cancellationToken);

            if (isPostAuthorBlocked)
            {
                _logger.LogInformation("reply.get: Viewer {ViewerId} blocked with post author {AuthorId}",
                    request.ViewerUserId.Value, post.AuthorId);
                throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
            }

            blockedUserIds = await _blockIsolationService.GetBidirectionalBlockedUserIdsAsync(
                request.ViewerUserId.Value,
                cancellationToken);
        }

        // 3. Fetch all conversation replies for the post
        var allReplies = await _dbContext.PostReplies
            .AsNoTracking()
            .Include(r => r.Author)
            .Where(r => r.PostId == request.PostId)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        // 4. Build hierarchical tree with depth-first traversal
        var lookupByParent = allReplies
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToLookup(r => r.ParentReplyId);

        var flattenedHierarchy = new List<ReplyResponse>();

        void Traverse(Guid? parentId, int depth)
        {
            var children = lookupByParent[parentId];
            if (!children.Any())
            {
                return;
            }

            foreach (var reply in children)
            {
                // Check if reply has children in the thread
                bool hasChildren = lookupByParent[reply.Id].Any();

                // If user is blocked, skip this branch unless there are visible child replies
                bool isAuthorBlocked = blockedUserIds.Contains(reply.AuthorId);
                bool isAuthorSuspended = reply.Author?.IsSuspended ?? false;

                if (reply.IsDeleted)
                {
                    // Soft-deleted replies: include placeholder if they preserve children
                    if (hasChildren)
                    {
                        flattenedHierarchy.Add(new ReplyResponse(
                            reply.Id,
                            reply.PostId,
                            reply.ParentReplyId,
                            "[This reply was deleted by the author]",
                            null,
                            depth,
                            true,
                            reply.CreatedAt,
                            reply.UpdatedAt
                        ));
                    }
                }
                else if (isAuthorSuspended)
                {
                    // Suspended author: include placeholder if preserving child threads
                    if (hasChildren)
                    {
                        flattenedHierarchy.Add(new ReplyResponse(
                            reply.Id,
                            reply.PostId,
                            reply.ParentReplyId,
                            "[Post is deleted]",
                            null,
                            depth,
                            true,
                            reply.CreatedAt,
                            reply.UpdatedAt
                        ));
                    }
                }
                else if (isAuthorBlocked)
                {
                    // Blocked author: include placeholder if preserving child threads
                    if (hasChildren)
                    {
                        flattenedHierarchy.Add(new ReplyResponse(
                            reply.Id,
                            reply.PostId,
                            reply.ParentReplyId,
                            "[This content is from a blocked account]",
                            null,
                            depth,
                            false,
                            reply.CreatedAt,
                            reply.UpdatedAt
                        ));
                    }
                }
                else
                {
                    var authorDto = reply.Author != null
                        ? new ReplyAuthorDto(
                            reply.Author.Id,
                            reply.Author.Username,
                            reply.Author.DisplayName,
                            reply.Author.AvatarUrl,
                            reply.Author.IsVerified)
                        : null;

                    flattenedHierarchy.Add(new ReplyResponse(
                        reply.Id,
                        reply.PostId,
                        reply.ParentReplyId,
                        reply.Content,
                        authorDto,
                        depth,
                        false,
                        reply.CreatedAt,
                        reply.UpdatedAt
                    ));
                }

                // Recurse down children
                Traverse(reply.Id, depth + 1);
            }
        }

        // Start DFS from root replies (ParentReplyId == null)
        Traverse(null, 0);

        // 5. Cursor Pagination over flattened hierarchical sequence
        int startIndex = 0;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var decodedCursor = Cursor.Decode(request.Cursor);
            if (decodedCursor != null)
            {
                var matchIndex = flattenedHierarchy.FindIndex(r => r.Id == decodedCursor.Id);
                if (matchIndex >= 0)
                {
                    startIndex = matchIndex + 1;
                }
            }
        }

        var pagedItems = flattenedHierarchy.Skip(startIndex).Take(pageSize + 1).ToList();
        bool hasNextPage = pagedItems.Count > pageSize;
        var resultItems = hasNextPage ? pagedItems.Take(pageSize).ToList() : pagedItems;

        string? nextCursor = null;
        if (hasNextPage && resultItems.Count > 0)
        {
            var lastItem = resultItems[^1];
            nextCursor = new Cursor(lastItem.CreatedAt, lastItem.Id).Encode();
        }

        return new HierarchicalRepliesResult(
            resultItems,
            nextCursor,
            hasNextPage,
            pageSize
        );
    }
}
