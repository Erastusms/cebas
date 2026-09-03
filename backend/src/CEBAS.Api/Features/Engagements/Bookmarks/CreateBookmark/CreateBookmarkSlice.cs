using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Engagements;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Engagements.Bookmarks.CreateBookmark;

public sealed record CreateBookmarkCommand(Guid ActorUserId, Guid PostId) : IRequest<BookmarkResponse>;

public sealed class CreateBookmarkCommandValidator : AbstractValidator<CreateBookmarkCommand>
{
    public CreateBookmarkCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");

        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("Post ID is required.");
    }
}

public sealed class CreateBookmarkCommandHandler : IRequestHandler<CreateBookmarkCommand, BookmarkResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<CreateBookmarkCommandHandler> _logger;

    public CreateBookmarkCommandHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<CreateBookmarkCommandHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<BookmarkResponse> Handle(CreateBookmarkCommand request, CancellationToken cancellationToken)
    {
        // 1. Post existence and eligibility check
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, cancellationToken);

        if (post == null)
        {
            _logger.LogWarning("bookmark.create.failed: Post {PostId} not found or deleted for actor {ActorUserId}",
                request.PostId, request.ActorUserId);
            throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
        }

        // 2. Bidirectional block check
        var isBlocked = await _blockIsolationService.IsBlockedBidirectionalAsync(
            request.ActorUserId, post.AuthorId, cancellationToken);
        if (isBlocked)
        {
            _logger.LogWarning("bookmark.create.failed: Block isolation violation between {ActorUserId} and post author {AuthorId}",
                request.ActorUserId, post.AuthorId);
            throw new ForbiddenException("Cannot interact with this post due to privacy or block restrictions.");
        }

        // 3. Idempotent check
        var existing = await _dbContext.PostBookmarks
            .AsNoTracking()
            .AnyAsync(b => b.PostId == request.PostId && b.UserId == request.ActorUserId, cancellationToken);

        if (existing)
        {
            return new BookmarkResponse(request.PostId, Bookmarked: true, post.BookmarkCount);
        }

        // 4. Create bookmark and atomically increment counter
        var bookmark = PostBookmark.Create(request.PostId, request.ActorUserId);
        post.IncrementBookmarkCount();

        try
        {
            await _dbContext.PostBookmarks.AddAsync(bookmark, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("bookmark.created: Actor {ActorUserId} bookmarked post {PostId}, bookmark_count={BookmarkCount}",
                request.ActorUserId, request.PostId, post.BookmarkCount);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrent duplicate bookmark detected: Actor {ActorUserId} -> Post {PostId}",
                request.ActorUserId, request.PostId);

            _dbContext.ChangeTracker.Clear();

            var currentCount = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == request.PostId)
                .Select(p => p.BookmarkCount)
                .FirstOrDefaultAsync(cancellationToken);

            return new BookmarkResponse(request.PostId, Bookmarked: true, currentCount);
        }

        return new BookmarkResponse(request.PostId, Bookmarked: true, post.BookmarkCount);
    }
}

public sealed class PostBookmarkedEventHandler : INotificationHandler<PostBookmarkedDomainEvent>
{
    private readonly ILogger<PostBookmarkedEventHandler> _logger;

    public PostBookmarkedEventHandler(ILogger<PostBookmarkedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PostBookmarkedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] PostBookmarked: Actor {ActorUserId} bookmarked Post {PostId} at {OccurredAt}",
            notification.ActorUserId, notification.PostId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
