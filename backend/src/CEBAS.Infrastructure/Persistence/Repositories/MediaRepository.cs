using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Repositories;

public class MediaRepository : IMediaRepository
{
    private readonly ApplicationDbContext _dbContext;

    public MediaRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Media?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task AddAsync(Media media, CancellationToken cancellationToken = default)
    {
        await _dbContext.Media.AddAsync(media, cancellationToken);
    }

    public Task UpdateAsync(Media media, CancellationToken cancellationToken = default)
    {
        _dbContext.Media.Update(media);
        return Task.CompletedTask;
    }

    public async Task<List<Media>> GetByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Media
            .AsNoTracking()
            .Where(m => m.OwnerUserId == ownerUserId && m.Status != MediaStatus.Deleted)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
