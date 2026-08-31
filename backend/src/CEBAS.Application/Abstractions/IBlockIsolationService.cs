namespace CEBAS.Application.Abstractions;

/// <summary>
/// Service providing centralized, server-side block isolation checks and query filtering primitives.
/// </summary>
public interface IBlockIsolationService
{
    /// <summary>
    /// Checks if a blocking relationship exists in either direction between userA and userB (userA -> userB OR userB -> userA).
    /// </summary>
    Task<bool> IsBlockedBidirectionalAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if blockerId has actively blocked blockedId (blocker -> blocked).
    /// </summary>
    Task<bool> HasBlockedAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all user IDs that have a bidirectional blocking relationship with the specified user.
    /// </summary>
    Task<HashSet<Guid>> GetBidirectionalBlockedUserIdsAsync(Guid userId, CancellationToken cancellationToken = default);
}
