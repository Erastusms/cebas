using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Search;

namespace CEBAS.Application.Abstractions.Search;

public interface ISearchService
{
    Task<CursorPagedResult<SearchPostItemDto>> SearchPostsAsync(SearchPostsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchUserItemDto>> SearchUsersAsync(SearchUsersQuery query, CancellationToken cancellationToken = default);
    Task<SearchSummaryResponse> SearchSummaryAsync(SearchSummaryQuery query, CancellationToken cancellationToken = default);
}
