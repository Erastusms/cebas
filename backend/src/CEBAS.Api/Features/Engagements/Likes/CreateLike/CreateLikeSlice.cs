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

namespace CEBAS.Api.Features.Engagements.Likes.CreateLike;

public sealed record CreateLikeCommand(Guid ActorUserId, Guid PostId) : IRequest<LikeResponse>;

public sealed class CreateLikeCommandValidator : AbstractValidator<CreateLikeCommand>
{
    public CreateLikeCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");

        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("Post ID is required.");
    }
}

public sealed class CreateLikeCommandHandler : IRequestHandler<CreateLikeCommand, LikeResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<CreateLikeCommandHandler> _logger;

    public CreateLikeCommandHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<CreateLikeCommandHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<LikeResponse> Handle(CreateLikeCommand request, CancellationToken cancellationToken)
    {
        // 1. Post existence and eligibility check
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, cancellationToken);

        if (post == null)
        {
            _logger.LogWarning("like.create.failed: Post {PostId} not found or deleted for actor {ActorUserId}",
                request.PostId, request.ActorUserId);
            throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
        }

        // 2. Bidirectional block check (server-side isolation enforcement)
        var isBlocked = await _blockIsolationService.IsBlockedBidirectionalAsync(
            request.ActorUserId, post.AuthorId, cancellationToken);
        if (isBlocked)
        {
            _logger.LogWarning("like.create.failed: Block isolation violation between {ActorUserId} and post author {AuthorId}",
                request.ActorUserId, post.AuthorId);
            throw new ForbiddenException("Cannot interact with this post due to privacy or block restrictions.");
        }

        // 3. Check for existing like (idempotent path)
        var existing = await _dbContext.PostLikes
            .AsNoTracking()
            .AnyAsync(l => l.PostId == request.PostId && l.UserId == request.ActorUserId, cancellationToken);

        if (existing)
        {
            // Idempotent: already liked, return current state without counter change
            return new LikeResponse(request.PostId, Liked: true, post.LikeCount);
        }

        // 4. Create like and atomically increment counter
        var like = PostLike.Create(request.PostId, request.ActorUserId);
        post.IncrementLikeCount();

        try
        {
            await _dbContext.PostLikes.AddAsync(like, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("like.created: Actor {ActorUserId} liked post {PostId}, like_count={LikeCount}",
                request.ActorUserId, request.PostId, post.LikeCount);
        }
        catch (DbUpdateException ex)
        {
            // Concurrent duplicate like — unique constraint violation handled idempotently
            _logger.LogWarning(ex, "Concurrent duplicate like detected: Actor {ActorUserId} -> Post {PostId}",
                request.ActorUserId, request.PostId);

            // Detach the entities we were trying to save to avoid stale state
            _dbContext.ChangeTracker.Clear();

            // Re-read the authoritative count
            var currentCount = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => p.Id == request.PostId)
                .Select(p => p.LikeCount)
                .FirstOrDefaultAsync(cancellationToken);

            return new LikeResponse(request.PostId, Liked: true, currentCount);
        }

        return new LikeResponse(request.PostId, Liked: true, post.LikeCount);
    }
}

public sealed class PostLikedEventHandler : INotificationHandler<PostLikedDomainEvent>
{
    private readonly ILogger<PostLikedEventHandler> _logger;

    public PostLikedEventHandler(ILogger<PostLikedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PostLikedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] PostLiked: Actor {ActorUserId} liked Post {PostId} at {OccurredAt}",
            notification.ActorUserId, notification.PostId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
