using CEBAS.Application.Contracts.Search;

namespace CEBAS.Application.Abstractions.Search;

public interface ISearchIndexManager
{
    Task EnsureIndicesExistAsync(CancellationToken cancellationToken = default);
    Task RecreateIndicesAsync(CancellationToken cancellationToken = default);
    Task<SearchHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default);
}
