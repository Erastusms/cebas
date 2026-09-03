using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Engagements;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Engagements.Bookmarks.RemoveBookmark;

public sealed record RemoveBookmarkCommand(Guid ActorUserId, Guid PostId) : IRequest<BookmarkResponse>;

public sealed class RemoveBookmarkCommandValidator : AbstractValidator<RemoveBookmarkCommand>
{
    public RemoveBookmarkCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");

        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("Post ID is required.");
    }
}

public sealed class RemoveBookmarkCommandHandler : IRequestHandler<RemoveBookmarkCommand, BookmarkResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RemoveBookmarkCommandHandler> _logger;

    public RemoveBookmarkCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<RemoveBookmarkCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BookmarkResponse> Handle(RemoveBookmarkCommand request, CancellationToken cancellationToken)
    {
        // 1. Post existence check
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post == null)
        {
            _logger.LogWarning("bookmark.remove.failed: Post {PostId} not found for actor {ActorUserId}",
                request.PostId, request.ActorUserId);
            throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
        }

        // 2. Find existing bookmark
        var existingBookmark = await _dbContext.PostBookmarks
            .FirstOrDefaultAsync(b => b.PostId == request.PostId && b.UserId == request.ActorUserId, cancellationToken);

        if (existingBookmark == null)
        {
            // Idempotent: already unbookmarked
            return new BookmarkResponse(request.PostId, Bookmarked: false, post.BookmarkCount);
        }

        // 3. Remove bookmark and atomically decrement counter
        _dbContext.PostBookmarks.Remove(existingBookmark);
        post.DecrementBookmarkCount();

        post.AddDomainEvent(new PostUnbookmarkedDomainEvent(request.PostId, request.ActorUserId, DateTimeOffset.UtcNow));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("bookmark.removed: Actor {ActorUserId} unbookmarked post {PostId}, bookmark_count={BookmarkCount}",
                request.ActorUserId, request.PostId, post.BookmarkCount);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrent duplicate unbookmark detected: Actor {ActorUserId} -> Post {PostId}",
                request.ActorUserId, request.PostId);

            _dbContext.ChangeTracker.Clear();

            var currentCount = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == request.PostId)
                .Select(p => p.BookmarkCount)
                .FirstOrDefaultAsync(cancellationToken);

            return new BookmarkResponse(request.PostId, Bookmarked: false, currentCount);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrent duplicate unbookmark detected: Actor {ActorUserId} -> Post {PostId}",
                request.ActorUserId, request.PostId);

            _dbContext.ChangeTracker.Clear();

            var currentCount = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == request.PostId)
                .Select(p => p.BookmarkCount)
                .FirstOrDefaultAsync(cancellationToken);

            return new BookmarkResponse(request.PostId, Bookmarked: false, currentCount);
        }

        return new BookmarkResponse(request.PostId, Bookmarked: false, post.BookmarkCount);
    }
}

public sealed class PostUnbookmarkedEventHandler : INotificationHandler<PostUnbookmarkedDomainEvent>
{
    private readonly ILogger<PostUnbookmarkedEventHandler> _logger;

    public PostUnbookmarkedEventHandler(ILogger<PostUnbookmarkedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PostUnbookmarkedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] PostUnbookmarked: Actor {ActorUserId} unbookmarked Post {PostId} at {OccurredAt}",
            notification.ActorUserId, notification.PostId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
