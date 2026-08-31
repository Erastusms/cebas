using CEBAS.Domain.Common;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a directed block relationship (blocker -> blocked).
/// </summary>
public class Block : Entity
{
    public Guid BlockerId { get; private set; }
    public Guid BlockedId { get; private set; }

    // Navigation properties
    public User? Blocker { get; private set; }
    public User? Blocked { get; private set; }

    // EF Core parameterless constructor
    protected Block() { }

    public static Block Create(Guid blockerId, Guid blockedId)
    {
        if (blockerId == Guid.Empty)
        {
            throw new ValidationException("BlockerId", "Blocker user ID is required.");
        }

        if (blockedId == Guid.Empty)
        {
            throw new ValidationException("BlockedId", "Blocked user ID is required.");
        }

        if (blockerId == blockedId)
        {
            throw new ValidationException("Block", "A user cannot block themselves.");
        }

        var block = new Block
        {
            Id = Uuid7.New(),
            BlockerId = blockerId,
            BlockedId = blockedId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        block.AddDomainEvent(new UserBlockedDomainEvent(blockerId, blockedId, block.CreatedAt));
        return block;
    }
}
