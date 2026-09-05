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
using MediaEntity = CEBAS.Domain.Entities.Media;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Posts.CreatePost;

public sealed record CreatePostCommand(
    Guid AuthorUserId,
    string? Content,
    List<Guid>? MediaIds
) : IRequest<PostResponse>;

public sealed class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(x => x.AuthorUserId)
            .NotEmpty().WithMessage("Author user ID is required.");

        RuleFor(x => x.Content)
            .MaximumLength(Post.MaxContentLength)
            .WithMessage($"Post content cannot exceed {Post.MaxContentLength} characters.");

        RuleFor(x => x.MediaIds)
            .Must(m => m == null || m.Count <= Post.MaxMediaAttachments)
            .WithMessage($"A post cannot attach more than {Post.MaxMediaAttachments} media assets.")
            .Must(m => m == null || m.Distinct().Count() == m.Count)
            .WithMessage("Duplicate media attachments are not allowed.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content) || (x.MediaIds != null && x.MediaIds.Count > 0))
            .WithMessage("A post cannot be empty. Please provide text content or attach at least one media image.");
    }
}

public sealed class CreatePostCommandHandler : IRequestHandler<CreatePostCommand, PostResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxWriter? _outboxWriter;
    private readonly ILogger<CreatePostCommandHandler> _logger;

    public CreatePostCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<CreatePostCommandHandler> logger)
        : this(dbContext, null, logger)
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public CreatePostCommandHandler(
        ApplicationDbContext dbContext,
        IOutboxWriter? outboxWriter,
        ILogger<CreatePostCommandHandler> logger)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task<PostResponse> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        // 1. Author existence validation
        var author = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.AuthorUserId, cancellationToken);

        if (author == null)
        {
            _logger.LogWarning("post.create.failed: Author user {AuthorUserId} not found", request.AuthorUserId);
            throw new NotFoundException($"Author with ID '{request.AuthorUserId}' was not found.");
        }

        var mediaIds = request.MediaIds ?? new List<Guid>();

        // 2. Validate media attachments (ownership, readiness, uniqueness)
        List<MediaEntity> mediaEntities = new();
        if (mediaIds.Count > 0)
        {
            if (mediaIds.Count > Post.MaxMediaAttachments)
            {
                throw new ValidationException("MediaIds", $"A post cannot attach more than {Post.MaxMediaAttachments} media assets.");
            }

            if (mediaIds.Distinct().Count() != mediaIds.Count)
            {
                throw new ValidationException("MediaIds", "Duplicate media attachments are not allowed.");
            }

            var fetchedMedia = await _dbContext.Media
                .Where(m => mediaIds.Contains(m.Id))
                .ToListAsync(cancellationToken);

            if (fetchedMedia.Count != mediaIds.Count)
            {
                _logger.LogWarning("post.media.attach.failed: One or more media IDs not found for user {AuthorUserId}", request.AuthorUserId);
                throw new NotFoundException("One or more referenced media attachments were not found.");
            }

            // Order fetched media according to input list
            var mediaLookup = fetchedMedia.ToDictionary(m => m.Id);
            foreach (var id in mediaIds)
            {
                var media = mediaLookup[id];

                // Verify ownership (server-side security boundary)
                if (media.OwnerUserId != request.AuthorUserId)
                {
                    _logger.LogWarning("post.media.attach.failed: User {AuthorUserId} attempted to attach media {MediaId} owned by {OwnerUserId}",
                        request.AuthorUserId, media.Id, media.OwnerUserId);
                    throw new ForbiddenException("Cannot attach media owned by another user.");
                }

                // Verify media readiness
                if (media.Status != MediaStatus.Ready)
                {
                    _logger.LogWarning("post.media.attach.failed: Media {MediaId} is in status '{Status}' (expected READY)", media.Id, media.Status);
                    throw new ValidationException("MediaIds", $"Media asset '{media.Id}' is not ready for attachment (status: {media.Status}).");
                }

                mediaEntities.Add(media);
            }
        }

        // 3. ACID Transactional persistence
        var post = Post.Create(request.AuthorUserId, request.Content, mediaEntities.Count);

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _logger.LogInformation("post.create.started: Author {AuthorUserId} creating post with {MediaCount} media",
                    request.AuthorUserId, mediaEntities.Count);

                await _dbContext.Posts.AddAsync(post, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (mediaEntities.Count > 0)
                {
                    for (int i = 0; i < mediaEntities.Count; i++)
                    {
                        var postMedia = PostMedia.Create(post.Id, mediaEntities[i].Id, i);
                        await _dbContext.PostMedia.AddAsync(postMedia, cancellationToken);
                    }
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                // Append transactional outbox event
                if (_outboxWriter != null)
                {
                    await _outboxWriter.EnqueueAsync(
                        eventType: "POST_CREATED",
                        aggregateType: "Post",
                        aggregateId: post.Id,
                        payload: new PostCreatedPayload(
                            post.Id,
                            post.AuthorId,
                            post.Content,
                            post.MediaCount,
                            post.CreatedAt
                        ),
                        actorId: post.AuthorId,
                        cancellationToken: cancellationToken
                    );
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("post.create.succeeded: Post {PostId} published by author {AuthorUserId}",
                    post.Id, request.AuthorUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "post.create.failed: Failed to create post for author {AuthorUserId}", request.AuthorUserId);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        // 4. Construct response DTO
        var authorDto = new PostAuthorDto(
            author.Id,
            author.Username,
            author.DisplayName,
            author.AvatarUrl,
            author.IsVerified
        );

        var mediaDtos = mediaEntities.Select((m, index) => new PostMediaDto(
            m.Id,
            $"/api/v1/media/{m.Id}",
            m.OriginalFileName,
            m.MimeType,
            index
        )).ToList();

        return new PostResponse(
            post.Id,
            post.Content,
            authorDto,
            mediaDtos,
            post.ReplyCount,
            post.MediaCount,
            post.LikeCount,
            post.BookmarkCount,
            false,
            false,
            post.IsDeleted,
            post.CreatedAt,
            post.UpdatedAt
        );
    }
}

public sealed class PostCreatedEventHandler : INotificationHandler<PostCreatedDomainEvent>
{
    private readonly ILogger<PostCreatedEventHandler> _logger;

    public PostCreatedEventHandler(ILogger<PostCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PostCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] PostCreated: Post {PostId} by Author {AuthorId} at {OccurredAt}",
            notification.PostId, notification.AuthorId, notification.OccurredAt);

        // Downstream timeline fanout, search indexing, and metrics can hook in here asynchronously
        return Task.CompletedTask;
    }
}
