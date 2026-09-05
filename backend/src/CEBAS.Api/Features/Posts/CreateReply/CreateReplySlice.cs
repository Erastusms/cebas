using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Events;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Posts.CreateReply;

public sealed record CreateReplyCommand(
    Guid PostId,
    Guid AuthorUserId,
    string Content,
    Guid? ParentReplyId
) : IRequest<ReplyResponse>;

public sealed class CreateReplyCommandValidator : AbstractValidator<CreateReplyCommand>
{
    public CreateReplyCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty().WithMessage("Post ID is required.");

        RuleFor(x => x.AuthorUserId)
            .NotEmpty().WithMessage("Author user ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Reply content cannot be empty.")
            .MaximumLength(PostReply.MaxContentLength)
            .WithMessage($"Reply content cannot exceed {PostReply.MaxContentLength} characters.");
    }
}

public sealed class CreateReplyCommandHandler : IRequestHandler<CreateReplyCommand, ReplyResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly IOutboxWriter? _outboxWriter;
    private readonly ILogger<CreateReplyCommandHandler> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public CreateReplyCommandHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        IOutboxWriter? outboxWriter,
        ILogger<CreateReplyCommandHandler> logger)
    {
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public CreateReplyCommandHandler(
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        ILogger<CreateReplyCommandHandler> logger)
        : this(dbContext, blockIsolationService, null, logger)
    {
    }

    public async Task<ReplyResponse> Handle(CreateReplyCommand request, CancellationToken cancellationToken)
    {
        // 1. Author existence
        var author = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.AuthorUserId, cancellationToken);

        if (author == null)
        {
            _logger.LogWarning("reply.create.failed: Author user {AuthorUserId} not found", request.AuthorUserId);
            throw new NotFoundException($"User with ID '{request.AuthorUserId}' was not found.");
        }

        if (author.IsSuspended)
        {
            _logger.LogWarning("reply.create.failed: Suspended user {AuthorUserId} attempted to create reply", request.AuthorUserId);
            throw new ForbiddenException("Your account has been suspended and cannot reply to posts.");
        }

        // 2. Post existence and state
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(p => p.Id == request.PostId, cancellationToken);

        if (post == null || post.IsDeleted)
        {
            _logger.LogWarning("reply.create.failed: Post {PostId} not found or deleted", request.PostId);
            throw new NotFoundException($"Post with ID '{request.PostId}' was not found.");
        }

        // Block isolation check between replying user and post author
        var isBlockedWithPostAuthor = await _blockIsolationService.IsBlockedBidirectionalAsync(
            request.AuthorUserId,
            post.AuthorId,
            cancellationToken);

        if (isBlockedWithPostAuthor)
        {
            _logger.LogWarning("reply.create.failed: Block restriction between author {AuthorUserId} and post author {PostAuthorId}",
                request.AuthorUserId, post.AuthorId);
            throw new ForbiddenException("Cannot reply to this post due to privacy or block restrictions.");
        }

        // 3. Parent reply validation (if nested reply)
        int depth = 0;
        PostReply? parentReply = null;
        if (request.ParentReplyId.HasValue)
        {
            parentReply = await _dbContext.PostReplies
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == request.ParentReplyId.Value, cancellationToken);

            if (parentReply == null)
            {
                _logger.LogWarning("reply.create.failed: Parent reply {ParentReplyId} not found", request.ParentReplyId.Value);
                throw new NotFoundException($"Parent reply with ID '{request.ParentReplyId.Value}' was not found.");
            }

            // Invariant: Parent reply must belong to the exact same post
            if (parentReply.PostId != request.PostId)
            {
                _logger.LogWarning("reply.create.failed: Cross-post reply attempt. Parent {ParentReplyId} belongs to post {ParentPostId}, not {PostId}",
                    parentReply.Id, parentReply.PostId, request.PostId);
                throw new ValidationException("ParentReplyId", "Parent reply does not belong to the target conversation post.");
            }

            // Block check with parent reply author
            var isBlockedWithParentAuthor = await _blockIsolationService.IsBlockedBidirectionalAsync(
                request.AuthorUserId,
                parentReply.AuthorId,
                cancellationToken);

            if (isBlockedWithParentAuthor)
            {
                _logger.LogWarning("reply.create.failed: Block restriction with parent reply author {ParentAuthorId}",
                    parentReply.AuthorId);
                throw new ForbiddenException("Cannot reply to this comment due to privacy or block restrictions.");
            }
        }

        // 4. ACID Transaction persistence & atomic counter increment
        var reply = PostReply.Create(
            request.PostId,
            request.AuthorUserId,
            request.Content,
            request.ParentReplyId
        );

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _logger.LogInformation("reply.create.started: User {AuthorUserId} creating reply on Post {PostId} (Parent: {ParentReplyId})",
                    request.AuthorUserId, request.PostId, request.ParentReplyId);

                await _dbContext.PostReplies.AddAsync(reply, cancellationToken);

                // Atomic reply counter increment on post
                post.IncrementReplyCount();

                // 5. Create notification for post author if replying user is not the post author
                if (post.AuthorId != request.AuthorUserId)
                {
                    var postAuthorNotification = Notification.Create(
                        recipientId: post.AuthorId,
                        actorId: request.AuthorUserId,
                        type: NotificationType.PostReplied,
                        targetId: post.Id,
                        targetType: "POST",
                        metadata: $"{{\"replyId\":\"{reply.Id}\",\"postId\":\"{post.Id}\"}}"
                    );
                    await _dbContext.Notifications.AddAsync(postAuthorNotification, cancellationToken);

                    if (_outboxWriter != null)
                    {
                        await _outboxWriter.EnqueueAsync(
                            eventType: "NOTIFICATION_CREATED",
                            aggregateType: "Notification",
                            aggregateId: postAuthorNotification.Id,
                            payload: new NotificationCreatedPayload(
                                postAuthorNotification.Id,
                                postAuthorNotification.RecipientId,
                                postAuthorNotification.ActorId,
                                "POST_REPLIED",
                                post.Id,
                                "POST",
                                postAuthorNotification.CreatedAt
                            ),
                            actorId: request.AuthorUserId,
                            recipientId: post.AuthorId,
                            cancellationToken: cancellationToken
                        );
                    }
                }

                // If nested reply, notify parent reply author if different from replying user and post author
                if (parentReply != null &&
                    parentReply.AuthorId != request.AuthorUserId &&
                    parentReply.AuthorId != post.AuthorId)
                {
                    var parentAuthorNotification = Notification.Create(
                        recipientId: parentReply.AuthorId,
                        actorId: request.AuthorUserId,
                        type: NotificationType.PostReplied,
                        targetId: post.Id,
                        targetType: "POST",
                        metadata: $"{{\"replyId\":\"{reply.Id}\",\"postId\":\"{post.Id}\",\"parentReplyId\":\"{parentReply.Id}\"}}"
                    );
                    await _dbContext.Notifications.AddAsync(parentAuthorNotification, cancellationToken);

                    if (_outboxWriter != null)
                    {
                        await _outboxWriter.EnqueueAsync(
                            eventType: "NOTIFICATION_CREATED",
                            aggregateType: "Notification",
                            aggregateId: parentAuthorNotification.Id,
                            payload: new NotificationCreatedPayload(
                                parentAuthorNotification.Id,
                                parentAuthorNotification.RecipientId,
                                parentAuthorNotification.ActorId,
                                "POST_REPLIED",
                                post.Id,
                                "POST",
                                parentAuthorNotification.CreatedAt
                            ),
                            actorId: request.AuthorUserId,
                            recipientId: parentReply.AuthorId,
                            cancellationToken: cancellationToken
                        );
                    }
                }

                // 6. Enqueue REPLY_CREATED outbox event for real-time post comment distribution
                if (_outboxWriter != null)
                {
                    await _outboxWriter.EnqueueAsync(
                        eventType: "REPLY_CREATED",
                        aggregateType: "Post",
                        aggregateId: post.Id,
                        payload: new ReplyCreatedPayload(
                            reply.Id,
                            post.Id,
                            request.AuthorUserId,
                            request.ParentReplyId,
                            post.ReplyCount,
                            DateTimeOffset.UtcNow
                        ),
                        actorId: request.AuthorUserId,
                        cancellationToken: cancellationToken
                    );
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("reply.create.succeeded: Reply {ReplyId} created on Post {PostId}",
                    reply.Id, request.PostId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "reply.create.failed: Failed to create reply on Post {PostId}", request.PostId);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        var authorDto = new ReplyAuthorDto(
            author.Id,
            author.Username,
            author.DisplayName,
            author.AvatarUrl,
            author.IsVerified
        );

        return new ReplyResponse(
            reply.Id,
            reply.PostId,
            reply.ParentReplyId,
            reply.Content,
            authorDto,
            depth,
            reply.IsDeleted,
            reply.CreatedAt,
            reply.UpdatedAt
        );
    }
}

public sealed class ReplyCreatedEventHandler : INotificationHandler<ReplyCreatedDomainEvent>
{
    private readonly ILogger<ReplyCreatedEventHandler> _logger;

    public ReplyCreatedEventHandler(ILogger<ReplyCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ReplyCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] ReplyCreated: Reply {ReplyId} on Post {PostId} by Author {AuthorId} (Parent: {ParentReplyId}) at {OccurredAt}",
            notification.ReplyId, notification.PostId, notification.AuthorId, notification.ParentReplyId, notification.OccurredAt);

        return Task.CompletedTask;
    }
}
