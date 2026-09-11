using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Contracts.Search;

namespace CEBAS.Api.Features.Search.SearchUsers;

public sealed record SearchUsersRequestQuery(
    string? Query,
    int Limit = 20,
    Guid? CurrentUserId = null
) : IRequest<IReadOnlyList<SearchUserItemDto>>;

public sealed class SearchUsersRequestQueryValidator : AbstractValidator<SearchUsersRequestQuery>
{
    public SearchUsersRequestQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50).WithMessage("Limit must be between 1 and 50.");
    }
}

public sealed class SearchUsersQueryHandler : IRequestHandler<SearchUsersRequestQuery, IReadOnlyList<SearchUserItemDto>>
{
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchUsersQueryHandler> _logger;

    public SearchUsersQueryHandler(ISearchService searchService, ILogger<SearchUsersQueryHandler> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchUserItemDto>> Handle(SearchUsersRequestQuery request, CancellationToken cancellationToken)
    {
        return await _searchService.SearchUsersAsync(new Application.Contracts.Search.SearchUsersQuery(
            request.Query,
            request.Limit,
            request.CurrentUserId
        ), cancellationToken);
    }
}
