using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Search;

namespace CEBAS.Api.Features.Search.SearchPosts;

public sealed record SearchPostsRequestQuery(
    string? Query,
    string? Cursor = null,
    int Limit = 20,
    Guid? CurrentUserId = null
) : IRequest<CursorPagedResult<SearchPostItemDto>>;

public sealed class SearchPostsRequestQueryValidator : AbstractValidator<SearchPostsRequestQuery>
{
    public SearchPostsRequestQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50).WithMessage("Limit must be between 1 and 50.");
    }
}

public sealed class SearchPostsQueryHandler : IRequestHandler<SearchPostsRequestQuery, CursorPagedResult<SearchPostItemDto>>
{
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchPostsQueryHandler> _logger;

    public SearchPostsQueryHandler(ISearchService searchService, ILogger<SearchPostsQueryHandler> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<CursorPagedResult<SearchPostItemDto>> Handle(SearchPostsRequestQuery request, CancellationToken cancellationToken)
    {
        return await _searchService.SearchPostsAsync(new Application.Contracts.Search.SearchPostsQuery(
            request.Query,
            request.Cursor,
            request.Limit,
            request.CurrentUserId
        ), cancellationToken);
    }
}
