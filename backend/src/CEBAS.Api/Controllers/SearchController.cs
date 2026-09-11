using MediatR;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Features.Search.ReindexSearch;
using CEBAS.Api.Features.Search.SearchPosts;
using CEBAS.Api.Features.Search.SearchSummary;
using CEBAS.Api.Features.Search.SearchUsers;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Search;

namespace CEBAS.Api.Controllers;

[ApiController]
public class SearchController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public SearchController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Searches posts using Indonesian-aware full-text search, fuzzy matching, highlighting, and keyset cursor pagination.
    /// </summary>
    [HttpGet("api/v1/search/posts")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<SearchPostItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPosts(
        [FromQuery] string? q,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchPostsRequestQuery(q, cursor, limit, _currentUser.UserId);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<SearchPostItemDto>>.Ok(result, "Search results retrieved successfully."));
    }

    /// <summary>
    /// Searches users with exact/prefix handle matching, fuzzy display name/bio matching, and autocomplete.
    /// </summary>
    [HttpGet("api/v1/search/users")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SearchUserItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] string? q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchUsersRequestQuery(q, limit, _currentUser.UserId);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SearchUserItemDto>>.Ok(result, "Users retrieved successfully."));
    }

    /// <summary>
    /// Combined discovery search returning matching posts and users for universal discovery.
    /// </summary>
    [HttpGet("api/v1/search")]
    [ProducesResponseType(typeof(ApiResponse<SearchSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchSummary(
        [FromQuery] string? q,
        [FromQuery] int postLimit = 5,
        [FromQuery] int userLimit = 5,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchSummaryRequestQuery(q, postLimit, userLimit, _currentUser.UserId);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<SearchSummaryResponse>.Ok(result, "Discovery search results retrieved successfully."));
    }

    /// <summary>
    /// Triggers bulk reindexing of PostgreSQL data into Elasticsearch.
    /// </summary>
    [HttpPost("api/v1/search/reindex")]
    [ProducesResponseType(typeof(ApiResponse<ReindexSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reindex(
        [FromQuery] string type = "all",
        [FromQuery] int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var command = new ReindexSearchCommand(type, batchSize);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<ReindexSummary>.Ok(result, "Search reindexing completed successfully."));
    }
}
