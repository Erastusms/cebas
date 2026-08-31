using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.SocialGraph;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.SocialGraph.UnblockUser;

public sealed record UnblockUserCommand(Guid BlockerUserId, Guid TargetUserId) : IRequest<BlockResponse>;

public sealed class UnblockUserCommandValidator : AbstractValidator<UnblockUserCommand>
{
    public UnblockUserCommandValidator()
    {
        RuleFor(x => x.BlockerUserId)
            .NotEmpty().WithMessage("Blocker user ID is required.");

        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Target user ID is required.");
    }
}

public sealed class UnblockUserCommandHandler : IRequestHandler<UnblockUserCommand, BlockResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UnblockUserCommandHandler> _logger;

    public UnblockUserCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<UnblockUserCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BlockResponse> Handle(UnblockUserCommand request, CancellationToken cancellationToken)
    {
        var targetExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (!targetExists)
        {
            _logger.LogWarning("unblock.failed: Target user {TargetUserId} not found for actor {BlockerUserId}",
                request.TargetUserId, request.BlockerUserId);
            throw new NotFoundException($"User with ID '{request.TargetUserId}' was not found.");
        }

        var block = await _dbContext.Blocks
            .FirstOrDefaultAsync(b => b.BlockerId == request.BlockerUserId && b.BlockedId == request.TargetUserId, cancellationToken);

        if (block != null)
        {
            _dbContext.Blocks.Remove(block);
            block.AddDomainEvent(new UserUnblockedDomainEvent(request.BlockerUserId, request.TargetUserId, DateTimeOffset.UtcNow));
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Social graph operation block.removed: Blocker {BlockerUserId} -> Blocked {TargetUserId}",
                request.BlockerUserId, request.TargetUserId);
        }

        // Invariant: Unblocking MUST NOT restore previously deleted follow relationships.
        return new BlockResponse(request.TargetUserId, IsBlocked: false, IsFollowing: false);
    }
}

public sealed class UserUnblockedEventHandler : INotificationHandler<UserUnblockedDomainEvent>
{
    private readonly ILogger<UserUnblockedEventHandler> _logger;

    public UserUnblockedEventHandler(ILogger<UserUnblockedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserUnblockedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] UserUnblocked: Blocker {BlockerId} -> Blocked {BlockedId} at {OccurredAt}",
            notification.BlockerId, notification.BlockedId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
