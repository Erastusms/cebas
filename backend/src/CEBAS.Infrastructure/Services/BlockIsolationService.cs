using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Abstractions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Infrastructure.Services;

public class BlockIsolationService : IBlockIsolationService
{
    private readonly ApplicationDbContext _dbContext;

    public BlockIsolationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsBlockedBidirectionalAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
    {
        if (userA == Guid.Empty || userB == Guid.Empty || userA == userB)
        {
            return false;
        }

        return await _dbContext.Blocks
            .AsNoTracking()
            .AnyAsync(b => (b.BlockerId == userA && b.BlockedId == userB) ||
                           (b.BlockerId == userB && b.BlockedId == userA),
                      cancellationToken);
    }

    public async Task<bool> HasBlockedAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
    {
        if (blockerId == Guid.Empty || blockedId == Guid.Empty || blockerId == blockedId)
        {
            return false;
        }

        return await _dbContext.Blocks
            .AsNoTracking()
            .AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, cancellationToken);
    }

    public async Task<HashSet<Guid>> GetBidirectionalBlockedUserIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return new HashSet<Guid>();
        }

        var blockedByMe = await _dbContext.Blocks
            .AsNoTracking()
            .Where(b => b.BlockerId == userId)
            .Select(b => b.BlockedId)
            .ToListAsync(cancellationToken);

        var blockingMe = await _dbContext.Blocks
            .AsNoTracking()
            .Where(b => b.BlockedId == userId)
            .Select(b => b.BlockerId)
            .ToListAsync(cancellationToken);

        var result = new HashSet<Guid>(blockedByMe);
        result.UnionWith(blockingMe);
        return result;
    }
}
