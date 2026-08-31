using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.SocialGraph;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.SocialGraph.UnfollowUser;

public sealed record UnfollowUserCommand(Guid ActorUserId, Guid TargetUserId) : IRequest<FollowResponse>;

public sealed class UnfollowUserCommandValidator : AbstractValidator<UnfollowUserCommand>
{
    public UnfollowUserCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");

        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Target user ID is required.");
    }
}

public sealed class UnfollowUserCommandHandler : IRequestHandler<UnfollowUserCommand, FollowResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly ILogger<UnfollowUserCommandHandler> _logger;

    public UnfollowUserCommandHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<UnfollowUserCommandHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _logger = logger;
    }

    public async Task<FollowResponse> Handle(UnfollowUserCommand request, CancellationToken cancellationToken)
    {
        var targetExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (!targetExists)
        {
            _logger.LogWarning("unfollow.failed: Target user {TargetUserId} not found for actor {ActorUserId}", request.TargetUserId, request.ActorUserId);
            throw new NotFoundException($"User with ID '{request.TargetUserId}' was not found.");
        }

        var existing = await _dbContext.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == request.ActorUserId && f.FollowingId == request.TargetUserId, cancellationToken);

        if (existing != null)
        {
            _dbContext.Follows.Remove(existing);
            existing.AddDomainEvent(new UserUnfollowedDomainEvent(request.ActorUserId, request.TargetUserId, DateTimeOffset.UtcNow));
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Social graph operation follow.removed: Actor {ActorUserId} -> Target {TargetUserId}",
                request.ActorUserId, request.TargetUserId);
        }

        var isBlocked = await _blockIsolationService.HasBlockedAsync(request.ActorUserId, request.TargetUserId, cancellationToken);

        return new FollowResponse(request.TargetUserId, IsFollowing: false, IsBlocked: isBlocked);
    }
}

public sealed class UserUnfollowedEventHandler : INotificationHandler<UserUnfollowedDomainEvent>
{
    private readonly ILogger<UserUnfollowedEventHandler> _logger;

    public UserUnfollowedEventHandler(ILogger<UserUnfollowedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserUnfollowedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] UserUnfollowed: Follower {FollowerId} -> Following {FollowingId} at {OccurredAt}",
            notification.FollowerId, notification.FollowingId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
