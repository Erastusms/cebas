using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Contracts.Search;

namespace CEBAS.Api.Features.Search.SearchSummary;

public sealed record SearchSummaryRequestQuery(
    string? Query,
    int PostLimit = 5,
    int UserLimit = 5,
    Guid? CurrentUserId = null
) : IRequest<SearchSummaryResponse>;

public sealed class SearchSummaryRequestQueryValidator : FluentValidation.AbstractValidator<SearchSummaryRequestQuery>
{
    public SearchSummaryRequestQueryValidator()
    {
        RuleFor(x => x.PostLimit)
            .InclusiveBetween(1, 50).WithMessage("PostLimit must be between 1 and 50.");

        RuleFor(x => x.UserLimit)
            .InclusiveBetween(1, 50).WithMessage("UserLimit must be between 1 and 50.");
    }
}

public sealed class SearchSummaryQueryHandler : IRequestHandler<SearchSummaryRequestQuery, SearchSummaryResponse>
{
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchSummaryQueryHandler> _logger;

    public SearchSummaryQueryHandler(ISearchService searchService, ILogger<SearchSummaryQueryHandler> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<SearchSummaryResponse> Handle(SearchSummaryRequestQuery request, CancellationToken cancellationToken)
    {
        return await _searchService.SearchSummaryAsync(new SearchSummaryQuery(
            request.Query,
            request.PostLimit,
            request.UserLimit,
            request.CurrentUserId
        ), cancellationToken);
    }
}
