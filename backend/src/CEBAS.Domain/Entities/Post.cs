using CEBAS.Domain.Common;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing an individual short-form social post.
/// Supports up to 1000 characters of text, up to 4 media attachments, and threaded replies.
/// </summary>
public class Post : Entity
{
    public const int MaxContentLength = 1000;
    public const int MaxMediaAttachments = 4;

    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; } = false;
    public DateTimeOffset? DeletedAt { get; private set; }
    public bool IsHidden { get; private set; } = false;
    public DateTimeOffset? HiddenAt { get; private set; }
    public string? HiddenReason { get; private set; }

    public int ReplyCount { get; private set; } = 0;
    public int MediaCount { get; private set; } = 0;
    public int LikeCount { get; private set; } = 0;
    public int BookmarkCount { get; private set; } = 0;

    // Navigation properties
    public User? Author { get; private set; }
    public ICollection<PostMedia> MediaAttachments { get; private set; } = new List<PostMedia>();
    public ICollection<PostReply> Replies { get; private set; } = new List<PostReply>();
    public ICollection<PostLike> Likes { get; private set; } = new List<PostLike>();
    public ICollection<PostBookmark> Bookmarks { get; private set; } = new List<PostBookmark>();

    // EF Core parameterless constructor
    protected Post() { }

    public static Post Create(Guid authorId, string? content, int mediaCount = 0)
    {
        if (authorId == Guid.Empty)
        {
            throw new ValidationException("AuthorId", "Author user ID cannot be empty.");
        }

        var trimmedContent = content?.Trim() ?? string.Empty;

        if (trimmedContent.Length > MaxContentLength)
        {
            throw new ValidationException("Content", $"Post content cannot exceed {MaxContentLength} characters.");
        }

        if (mediaCount < 0 || mediaCount > MaxMediaAttachments)
        {
            throw new ValidationException("MediaCount", $"Post media attachments must be between 0 and {MaxMediaAttachments}.");
        }

        if (string.IsNullOrWhiteSpace(trimmedContent) && mediaCount == 0)
        {
            throw new ValidationException("Content", "Post cannot be empty. Please provide text content or attach at least one media image.");
        }

        var post = new Post
        {
            Id = Uuid7.New(),
            AuthorId = authorId,
            Content = trimmedContent,
            MediaCount = mediaCount,
            ReplyCount = 0,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        post.AddDomainEvent(new PostCreatedDomainEvent(post.Id, post.AuthorId, post.CreatedAt));
        return post;
    }

    public void Delete()
    {
        if (IsDeleted)
        {
            // Idempotent deletion
            return;
        }

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PostDeletedDomainEvent(Id, AuthorId, DeletedAt.Value));
    }

    public void IncrementReplyCount()
    {
        ReplyCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DecrementReplyCount()
    {
        ReplyCount = Math.Max(0, ReplyCount - 1);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void IncrementLikeCount()
    {
        LikeCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DecrementLikeCount()
    {
        LikeCount = Math.Max(0, LikeCount - 1);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void IncrementBookmarkCount()
    {
        BookmarkCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DecrementBookmarkCount()
    {
        BookmarkCount = Math.Max(0, BookmarkCount - 1);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Hide(string reason)
    {
        if (IsHidden)
        {
            return;
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? "Post hidden by moderator." : reason.Trim();
        IsHidden = true;
        HiddenAt = DateTimeOffset.UtcNow;
        HiddenReason = trimmedReason;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PostHiddenDomainEvent(Id, AuthorId, HiddenReason, HiddenAt.Value));
    }

    public void Restore()
    {
        if (!IsHidden)
        {
            return;
        }

        IsHidden = false;
        HiddenAt = null;
        HiddenReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PostRestoredDomainEvent(Id, AuthorId, UpdatedAt.Value));
    }
}
