using FluentValidation;
using MediatR;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Trending;

namespace CEBAS.Api.Features.Hashtags.GetTrendingTopics;

public sealed record GetTrendingTopicsQuery(int Limit = 10) : IRequest<TrendingTopicsResult>;

public sealed class GetTrendingTopicsQueryValidator : AbstractValidator<GetTrendingTopicsQuery>
{
    public GetTrendingTopicsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Trending limit must be between 1 and 50.");
    }
}

public sealed class GetTrendingTopicsQueryHandler : IRequestHandler<GetTrendingTopicsQuery, TrendingTopicsResult>
{
    private readonly ITrendingService _trendingService;

    public GetTrendingTopicsQueryHandler(ITrendingService trendingService)
    {
        _trendingService = trendingService;
    }

    public async Task<TrendingTopicsResult> Handle(GetTrendingTopicsQuery request, CancellationToken cancellationToken)
    {
        return await _trendingService.GetTopTrendsAsync(request.Limit, cancellationToken);
    }
}
