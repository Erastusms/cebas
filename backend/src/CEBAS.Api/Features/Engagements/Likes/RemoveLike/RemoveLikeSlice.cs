using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Engagements;
using CEBAS.Application.Contracts.Events;
using CEBAS.Domain.Common;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Engagements.Likes.RemoveLike;

public sealed record RemoveLikeCommand(Guid ActorUserId, Guid PostId) : IRequest<LikeResponse>;

public sealed class RemoveLikeCommandValidator : AbstractValidator<RemoveLikeCommand>
{
    public RemoveLikeCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");

        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("Post ID is required.");
    }
}

public sealed class RemoveLikeCommandHandler : IRequestHandler<RemoveLikeCommand, LikeResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxWriter? _outboxWriter;
    private readonly ILogger<RemoveLikeCommandHandler> _logger;

    public RemoveLikeCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<RemoveLikeCommandHandler> logger)
        : this(dbContext, null, logger)
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public RemoveLikeCommandHandler(
        ApplicationDbContext dbContext,
        IOutboxWriter? outboxWriter,
        ILogger<RemoveLikeCommandHandler> logger)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task<LikeResponse> Handle(RemoveLikeCommand request, CancellationToken cancellationToken)
    {
        // 1. Post existence check
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post == null)
        {
            _logger.LogWarning("like.remove.failed: Post {PostId} not found for actor {ActorUserId}",
                request.PostId, request.ActorUserId);
            throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
        }

        // 2. Find existing like
        var existingLike = await _dbContext.PostLikes
            .FirstOrDefaultAsync(l => l.PostId == request.PostId && l.UserId == request.ActorUserId, cancellationToken);

        if (existingLike == null)
        {
            // Idempotent: already unliked, return current state without counter change
            return new LikeResponse(request.PostId, Liked: false, post.LikeCount);
        }

        // 3. Remove like and atomically decrement counter
        _dbContext.PostLikes.Remove(existingLike);
        post.DecrementLikeCount();

        // Raise domain event manually for unlike (entity is being removed, not created)
        post.AddDomainEvent(new PostUnlikedDomainEvent(request.PostId, request.ActorUserId, DateTimeOffset.UtcNow));

        // Enqueue POST_UNLIKED outbox event for real-time post counter sync
        if (_outboxWriter != null)
        {
            await _outboxWriter.EnqueueAsync(
                eventType: "POST_UNLIKED",
                aggregateType: "Post",
                aggregateId: post.Id,
                payload: new PostUnlikedPayload(
                    post.Id,
                    request.ActorUserId,
                    post.LikeCount,
                    DateTimeOffset.UtcNow
                ),
                actorId: request.ActorUserId,
                cancellationToken: cancellationToken
            );
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("like.removed: Actor {ActorUserId} unliked post {PostId}, like_count={LikeCount}",
                request.ActorUserId, request.PostId, post.LikeCount);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrent duplicate unlike detected: Actor {ActorUserId} -> Post {PostId}",
                request.ActorUserId, request.PostId);

            _dbContext.ChangeTracker.Clear();

            var currentCount = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == request.PostId)
                .Select(p => p.LikeCount)
                .FirstOrDefaultAsync(cancellationToken);

            return new LikeResponse(request.PostId, Liked: false, currentCount);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrent duplicate unlike detected: Actor {ActorUserId} -> Post {PostId}",
                request.ActorUserId, request.PostId);

            _dbContext.ChangeTracker.Clear();

            var currentCount = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == request.PostId)
                .Select(p => p.LikeCount)
                .FirstOrDefaultAsync(cancellationToken);

            return new LikeResponse(request.PostId, Liked: false, currentCount);
        }

        return new LikeResponse(request.PostId, Liked: false, post.LikeCount);
    }
}

public sealed class PostUnlikedEventHandler : INotificationHandler<PostUnlikedDomainEvent>
{
    private readonly ILogger<PostUnlikedEventHandler> _logger;

    public PostUnlikedEventHandler(ILogger<PostUnlikedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PostUnlikedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] PostUnliked: Actor {ActorUserId} unliked Post {PostId} at {OccurredAt}",
            notification.ActorUserId, notification.PostId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
