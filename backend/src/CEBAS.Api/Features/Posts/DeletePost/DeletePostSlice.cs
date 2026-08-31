using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Posts.DeletePost;

public sealed record DeletePostCommand(
    Guid PostId,
    Guid ActorUserId
) : IRequest<Unit>;

public sealed class DeletePostCommandValidator : AbstractValidator<DeletePostCommand>
{
    public DeletePostCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("Post ID is required.");

        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Actor user ID is required.");
    }
}

public sealed class DeletePostCommandHandler : IRequestHandler<DeletePostCommand, Unit>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DeletePostCommandHandler> _logger;

    public DeletePostCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<DeletePostCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post == null || post.IsDeleted)
        {
            _logger.LogWarning("post.delete.failed: Post {PostId} not found or already deleted", request.PostId);
            throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
        }

        // Server-side authorization: only the author may delete their post
        if (post.AuthorId != request.ActorUserId)
        {
            _logger.LogWarning("post.delete.failed: User {ActorUserId} unauthorized to delete post {PostId} owned by {AuthorId}",
                request.ActorUserId, post.Id, post.AuthorId);
            throw new ForbiddenException("Cannot delete a post created by another user.");
        }

        post.Delete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("post.delete.succeeded: Post {PostId} soft-deleted by owner {ActorUserId}",
            post.Id, request.ActorUserId);

        return Unit.Value;
    }
}

public sealed class PostDeletedEventHandler : INotificationHandler<PostDeletedDomainEvent>
{
    private readonly ILogger<PostDeletedEventHandler> _logger;

    public PostDeletedEventHandler(ILogger<PostDeletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PostDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] PostDeleted: Post {PostId} by Author {AuthorId} at {OccurredAt}",
            notification.PostId, notification.AuthorId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
