using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Posts.DeleteReply;

public sealed record DeleteReplyCommand(
    Guid ReplyId,
    Guid ActorUserId
) : IRequest<Unit>;

public sealed class DeleteReplyCommandValidator : AbstractValidator<DeleteReplyCommand>
{
    public DeleteReplyCommandValidator()
    {
        RuleFor(x => x.ReplyId)
            .NotEmpty().WithMessage("Reply ID is required.");

        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");
    }
}

public sealed class DeleteReplyCommandHandler : IRequestHandler<DeleteReplyCommand, Unit>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DeleteReplyCommandHandler> _logger;

    public DeleteReplyCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<DeleteReplyCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteReplyCommand request, CancellationToken cancellationToken)
    {
        var reply = await _dbContext.PostReplies
            .Include(r => r.Post)
            .FirstOrDefaultAsync(r => r.Id == request.ReplyId, cancellationToken);

        if (reply == null || reply.IsDeleted)
        {
            _logger.LogWarning("reply.delete.failed: Reply {ReplyId} not found or already deleted", request.ReplyId);
            throw new NotFoundException($"Reply with ID '{request.ReplyId}' was not found.");
        }

        // Server-side authorization check: only author may delete
        if (reply.AuthorId != request.ActorUserId)
        {
            _logger.LogWarning("reply.delete.failed: User {ActorUserId} unauthorized to delete reply {ReplyId} owned by {AuthorId}",
                request.ActorUserId, reply.Id, reply.AuthorId);
            throw new ForbiddenException("Cannot delete a reply created by another user.");
        }

        reply.Delete();

        // Decrement reply counter on post if active
        if (reply.Post != null)
        {
            reply.Post.DecrementReplyCount();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("reply.delete.succeeded: Reply {ReplyId} soft-deleted by owner {ActorUserId}",
            reply.Id, request.ActorUserId);

        return Unit.Value;
    }
}

public sealed class ReplyDeletedEventHandler : INotificationHandler<ReplyDeletedDomainEvent>
{
    private readonly ILogger<ReplyDeletedEventHandler> _logger;

    public ReplyDeletedEventHandler(ILogger<ReplyDeletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ReplyDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] ReplyDeleted: Reply {ReplyId} on Post {PostId} by Author {AuthorId} at {OccurredAt}",
            notification.ReplyId, notification.PostId, notification.AuthorId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
