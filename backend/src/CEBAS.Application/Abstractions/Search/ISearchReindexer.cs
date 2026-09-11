using CEBAS.Application.Contracts.Search;

namespace CEBAS.Application.Abstractions.Search;

public interface ISearchReindexer
{
    Task<ReindexSummary> ReindexAllAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task<ReindexSummary> ReindexPostsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task<ReindexSummary> ReindexUsersAsync(int batchSize = 100, CancellationToken cancellationToken = default);
}
