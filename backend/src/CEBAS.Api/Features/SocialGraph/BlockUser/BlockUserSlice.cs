using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.SocialGraph;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.SocialGraph.BlockUser;

public sealed record BlockUserCommand(Guid BlockerUserId, Guid TargetUserId) : IRequest<BlockResponse>;

public sealed class BlockUserCommandValidator : AbstractValidator<BlockUserCommand>
{
    public BlockUserCommandValidator()
    {
        RuleFor(x => x.BlockerUserId)
            .NotEmpty().WithMessage("Blocker user ID is required.");

        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Target user ID is required.");

        RuleFor(x => x)
            .Must(x => x.BlockerUserId != x.TargetUserId)
            .WithMessage("A user cannot block themselves.");
    }
}

public sealed class BlockUserCommandHandler : IRequestHandler<BlockUserCommand, BlockResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<BlockUserCommandHandler> _logger;

    public BlockUserCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<BlockUserCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BlockResponse> Handle(BlockUserCommand request, CancellationToken cancellationToken)
    {
        if (request.BlockerUserId == request.TargetUserId)
        {
            throw new ValidationException("Block", "A user cannot block themselves.");
        }

        // 1. Target user existence check
        var targetExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (!targetExists)
        {
            _logger.LogWarning("block.failed: Target user {TargetUserId} not found for actor {BlockerUserId}",
                request.TargetUserId, request.BlockerUserId);
            throw new NotFoundException($"User with ID '{request.TargetUserId}' was not found.");
        }

        // 2. Transactional execution: insert block and purge mutual follow relationships
        var existingBlock = await _dbContext.Blocks
            .FirstOrDefaultAsync(b => b.BlockerId == request.BlockerUserId && b.BlockedId == request.TargetUserId, cancellationToken);

        // Find any directed follow relationships in either direction (A -> B or B -> A)
        var followsToRemove = await _dbContext.Follows
            .Where(f => (f.FollowerId == request.BlockerUserId && f.FollowingId == request.TargetUserId) ||
                        (f.FollowerId == request.TargetUserId && f.FollowingId == request.BlockerUserId))
            .ToListAsync(cancellationToken);

        if (existingBlock == null)
        {
            var block = Block.Create(request.BlockerUserId, request.TargetUserId);
            await _dbContext.Blocks.AddAsync(block, cancellationToken);
        }

        if (followsToRemove.Count > 0)
        {
            _dbContext.Follows.RemoveRange(followsToRemove);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Social graph operation block.created: Blocker {BlockerUserId} -> Blocked {TargetUserId}. Follows removed: {Count}",
                request.BlockerUserId, request.TargetUserId, followsToRemove.Count);
        }
        catch (DbUpdateException ex)
        {
            // Idempotent protection against concurrent duplicate block creation
            _logger.LogWarning(ex, "Concurrent duplicate block detected: Blocker {BlockerUserId} -> Blocked {TargetUserId}",
                request.BlockerUserId, request.TargetUserId);
        }

        return new BlockResponse(request.TargetUserId, IsBlocked: true, IsFollowing: false);
    }
}

public sealed class UserBlockedEventHandler : INotificationHandler<UserBlockedDomainEvent>
{
    private readonly ILogger<UserBlockedEventHandler> _logger;

    public UserBlockedEventHandler(ILogger<UserBlockedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserBlockedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] UserBlocked: Blocker {BlockerId} -> Blocked {BlockedId} at {OccurredAt}",
            notification.BlockerId, notification.BlockedId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}

public sealed class FollowRelationshipsRemovedByBlockEventHandler : INotificationHandler<FollowRelationshipsRemovedByBlockDomainEvent>
{
    private readonly ILogger<FollowRelationshipsRemovedByBlockEventHandler> _logger;

    public FollowRelationshipsRemovedByBlockEventHandler(ILogger<FollowRelationshipsRemovedByBlockEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(FollowRelationshipsRemovedByBlockDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] FollowRelationshipsRemovedByBlock: Blocker {BlockerId}, Blocked {BlockedId}, Removed {Count} follows at {OccurredAt}",
            notification.BlockerId, notification.BlockedId, notification.FollowsRemovedCount, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
