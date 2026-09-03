using CEBAS.Domain.Common;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a user's like on a post.
/// Enforces one active like per user per post at the domain level.
/// </summary>
public class PostLike : Entity
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }

    // Navigation properties
    public Post? Post { get; private set; }
    public User? User { get; private set; }

    // EF Core parameterless constructor
    protected PostLike() { }

    public static PostLike Create(Guid postId, Guid userId)
    {
        if (postId == Guid.Empty)
        {
            throw new ValidationException("PostId", "Post ID is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new ValidationException("UserId", "User ID is required.");
        }

        var like = new PostLike
        {
            Id = Uuid7.New(),
            PostId = postId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        like.AddDomainEvent(new PostLikedDomainEvent(postId, userId, like.CreatedAt));
        return like;
    }
}
