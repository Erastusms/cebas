using CEBAS.Domain.Entities;

namespace CEBAS.Application.Abstractions;

public interface IMediaRepository
{
    Task<Media?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Media media, CancellationToken cancellationToken = default);
    Task UpdateAsync(Media media, CancellationToken cancellationToken = default);
    Task<List<Media>> GetByOwnerUserIdAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
}
