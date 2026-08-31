using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Junction entity representing an ordered media attachment on a post (positions 0 to 3).
/// </summary>
public class PostMedia : Entity
{
    public const int MinPosition = 0;
    public const int MaxPosition = 3;

    public Guid PostId { get; private set; }
    public Guid MediaId { get; private set; }
    public int Position { get; private set; }

    // Navigation properties
    public Post? Post { get; private set; }
    public Media? Media { get; private set; }

    // EF Core parameterless constructor
    protected PostMedia() { }

    public static PostMedia Create(Guid postId, Guid mediaId, int position)
    {
        if (postId == Guid.Empty)
        {
            throw new ValidationException("PostId", "Post ID cannot be empty.");
        }

        if (mediaId == Guid.Empty)
        {
            throw new ValidationException("MediaId", "Media ID cannot be empty.");
        }

        if (position < MinPosition || position > MaxPosition)
        {
            throw new ValidationException("Position", $"Media position must be between {MinPosition} and {MaxPosition}. Received: {position}.");
        }

        return new PostMedia
        {
            Id = Uuid7.New(),
            PostId = postId,
            MediaId = mediaId,
            Position = position,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
