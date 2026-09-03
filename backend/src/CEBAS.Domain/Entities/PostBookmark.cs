using CEBAS.Domain.Common;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a user's bookmark on a post.
/// Enforces one active bookmark per user per post at the domain level.
/// Bookmarks are private saved-state relationships.
/// </summary>
public class PostBookmark : Entity
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }

    // Navigation properties
    public Post? Post { get; private set; }
    public User? User { get; private set; }

    // EF Core parameterless constructor
    protected PostBookmark() { }

    public static PostBookmark Create(Guid postId, Guid userId)
    {
        if (postId == Guid.Empty)
        {
            throw new ValidationException("PostId", "Post ID is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new ValidationException("UserId", "User ID is required.");
        }

        var bookmark = new PostBookmark
        {
            Id = Uuid7.New(),
            PostId = postId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        bookmark.AddDomainEvent(new PostBookmarkedDomainEvent(postId, userId, bookmark.CreatedAt));
        return bookmark;
    }
}
