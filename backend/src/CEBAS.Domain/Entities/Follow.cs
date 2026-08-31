using CEBAS.Domain.Common;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a directed follow relationship (follower -> following).
/// </summary>
public class Follow : Entity
{
    public Guid FollowerId { get; private set; }
    public Guid FollowingId { get; private set; }

    // Navigation properties
    public User? Follower { get; private set; }
    public User? Following { get; private set; }

    // EF Core parameterless constructor
    protected Follow() { }

    public static Follow Create(Guid followerId, Guid followingId)
    {
        if (followerId == Guid.Empty)
        {
            throw new ValidationException("FollowerId", "Follower user ID is required.");
        }

        if (followingId == Guid.Empty)
        {
            throw new ValidationException("FollowingId", "Following user ID is required.");
        }

        if (followerId == followingId)
        {
            throw new ValidationException("Follow", "A user cannot follow themselves.");
        }

        var follow = new Follow
        {
            Id = Uuid7.New(),
            FollowerId = followerId,
            FollowingId = followingId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        follow.AddDomainEvent(new UserFollowedDomainEvent(followerId, followingId, follow.CreatedAt));
        return follow;
    }
}
