using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Junction entity representing the association between a Post and a Hashtag.
/// Guarantees referential integrity in PostgreSQL while high-frequency trending calculations
/// are absorbed by Redis.
/// </summary>
public class PostHashtag : Entity
{
    public Guid PostId { get; private set; }
    public Guid HashtagId { get; private set; }

    // Navigation properties
    public Post? Post { get; private set; }
    public Hashtag? Hashtag { get; private set; }

    // EF Core parameterless constructor
    protected PostHashtag() { }

    public static PostHashtag Create(Guid postId, Guid hashtagId)
    {
        if (postId == Guid.Empty)
        {
            throw new ValidationException("PostId", "Post ID cannot be empty.");
        }

        if (hashtagId == Guid.Empty)
        {
            throw new ValidationException("HashtagId", "Hashtag ID cannot be empty.");
        }

        return new PostHashtag
        {
            Id = Uuid7.New(),
            PostId = postId,
            HashtagId = hashtagId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
