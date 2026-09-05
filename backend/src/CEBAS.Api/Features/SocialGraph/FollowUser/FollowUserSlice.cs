using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Events;
using CEBAS.Application.Contracts.SocialGraph;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.SocialGraph.FollowUser;

public sealed record FollowUserCommand(Guid ActorUserId, Guid TargetUserId) : IRequest<FollowResponse>;

public sealed class FollowUserCommandValidator : AbstractValidator<FollowUserCommand>
{
    public FollowUserCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");

        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Target user ID is required.");

        RuleFor(x => x)
            .Must(x => x.ActorUserId != x.TargetUserId)
            .WithMessage("A user cannot follow themselves.");
    }
}

public sealed class FollowUserCommandHandler : IRequestHandler<FollowUserCommand, FollowResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly IOutboxWriter? _outboxWriter;
    private readonly ILogger<FollowUserCommandHandler> _logger;

    public FollowUserCommandHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<FollowUserCommandHandler> logger)
        : this(dbContext, blockIsolationService, null, logger)
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public FollowUserCommandHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        IOutboxWriter? outboxWriter,
        ILogger<FollowUserCommandHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }


    public async Task<FollowResponse> Handle(FollowUserCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == request.TargetUserId)
        {
            throw new ValidationException("Follow", "A user cannot follow themselves.");
        }

        var actor = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        if (actor != null && actor.IsSuspended)
        {
            throw new ForbiddenException("Your account has been suspended and cannot follow users.");
        }

        // 1. Target user existence check
        var targetExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (!targetExists)
        {
            _logger.LogWarning("follow.failed: Target user {TargetUserId} not found for actor {ActorUserId}", request.TargetUserId, request.ActorUserId);
            throw new NotFoundException($"User with ID '{request.TargetUserId}' was not found.");
        }

        // 2. Bidirectional block check (server-side isolation enforcement)
        var isBlocked = await _blockIsolationService.IsBlockedBidirectionalAsync(request.ActorUserId, request.TargetUserId, cancellationToken);
        if (isBlocked)
        {
            _logger.LogWarning("follow.failed: Block isolation violation between {ActorUserId} and {TargetUserId}", request.ActorUserId, request.TargetUserId);
            throw new ForbiddenException("Cannot follow this user due to privacy or block restrictions.");
        }

        // 3. Check for existing follow relationship
        var existing = await _dbContext.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == request.ActorUserId && f.FollowingId == request.TargetUserId, cancellationToken);

        if (existing != null)
        {
            return new FollowResponse(request.TargetUserId, IsFollowing: true, IsBlocked: false);
        }

        // 4. Create and persist follow relationship with database-level concurrency protection
        var follow = Follow.Create(request.ActorUserId, request.TargetUserId);

        try
        {
            await _dbContext.Follows.AddAsync(follow, cancellationToken);

            // 5. Create notification for target user
            var notification = Notification.Create(
                recipientId: request.TargetUserId,
                actorId: request.ActorUserId,
                type: NotificationType.UserFollowed,
                targetId: request.ActorUserId,
                targetType: "USER"
            );
            await _dbContext.Notifications.AddAsync(notification, cancellationToken);

            if (_outboxWriter != null)
            {
                await _outboxWriter.EnqueueAsync(
                    eventType: "NOTIFICATION_CREATED",
                    aggregateType: "Notification",
                    aggregateId: notification.Id,
                    payload: new NotificationCreatedPayload(
                        notification.Id,
                        notification.RecipientId,
                        notification.ActorId,
                        "USER_FOLLOWED",
                        notification.TargetId,
                        notification.TargetType,
                        notification.CreatedAt
                    ),
                    actorId: request.ActorUserId,
                    recipientId: request.TargetUserId,
                    cancellationToken: cancellationToken
                );

                // 6. Enqueue FOLLOW_CREATED outbox event
                await _outboxWriter.EnqueueAsync(
                    eventType: "FOLLOW_CREATED",
                    aggregateType: "Follow",
                    aggregateId: follow.Id,
                    payload: new FollowCreatedPayload(
                        request.ActorUserId,
                        request.TargetUserId,
                        DateTimeOffset.UtcNow
                    ),
                    actorId: request.ActorUserId,
                    recipientId: request.TargetUserId,
                    cancellationToken: cancellationToken
                );
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Social graph operation follow.created: Actor {ActorUserId} -> Target {TargetUserId}",
                request.ActorUserId, request.TargetUserId);
        }
        catch (DbUpdateException ex)
        {
            // Concurrent follow requests protection (handled idempotently)
            _logger.LogWarning(ex, "Concurrent duplicate follow detected: Actor {ActorUserId} -> Target {TargetUserId}",
                request.ActorUserId, request.TargetUserId);

            return new FollowResponse(request.TargetUserId, IsFollowing: true, IsBlocked: false);
        }

        return new FollowResponse(request.TargetUserId, IsFollowing: true, IsBlocked: false);
    }
}

public sealed class UserFollowedEventHandler : INotificationHandler<UserFollowedDomainEvent>
{
    private readonly ILogger<UserFollowedEventHandler> _logger;

    public UserFollowedEventHandler(ILogger<UserFollowedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserFollowedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] UserFollowed: Follower {FollowerId} -> Following {FollowingId} at {OccurredAt}",
            notification.FollowerId, notification.FollowingId, notification.OccurredAt);

        // Integration point for asynchronous notifications without coupling to atomic follow transaction
        return Task.CompletedTask;
    }
}
