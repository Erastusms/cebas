using CEBAS.Domain.Common;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a threaded reply attached to a post or nested parent reply.
/// </summary>
public class PostReply : Entity
{
    public const int MaxContentLength = 1000;

    public Guid PostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? ParentReplyId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; } = false;
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation properties
    public Post? Post { get; private set; }
    public User? Author { get; private set; }
    public PostReply? ParentReply { get; private set; }
    public ICollection<PostReply> ChildReplies { get; private set; } = new List<PostReply>();

    // EF Core parameterless constructor
    protected PostReply() { }

    public static PostReply Create(
        Guid postId,
        Guid authorId,
        string content,
        Guid? parentReplyId = null)
    {
        if (postId == Guid.Empty)
        {
            throw new ValidationException("PostId", "Post ID cannot be empty.");
        }

        if (authorId == Guid.Empty)
        {
            throw new ValidationException("AuthorId", "Author user ID cannot be empty.");
        }

        var trimmedContent = content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            throw new ValidationException("Content", "Reply content cannot be empty.");
        }

        if (trimmedContent.Length > MaxContentLength)
        {
            throw new ValidationException("Content", $"Reply content cannot exceed {MaxContentLength} characters.");
        }

        var reply = new PostReply
        {
            Id = Uuid7.New(),
            PostId = postId,
            AuthorId = authorId,
            ParentReplyId = parentReplyId,
            Content = trimmedContent,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        reply.AddDomainEvent(new ReplyCreatedDomainEvent(
            reply.Id,
            reply.PostId,
            reply.AuthorId,
            reply.ParentReplyId,
            reply.CreatedAt));

        return reply;
    }

    public void Delete()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ReplyDeletedDomainEvent(Id, PostId, AuthorId, DeletedAt.Value));
    }
}
