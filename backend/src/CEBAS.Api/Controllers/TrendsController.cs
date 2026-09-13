using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Features.Hashtags.GetTrendingTopics;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Trending;
using CEBAS.Infrastructure.Observability;

namespace CEBAS.Api.Controllers;

[ApiController]
public class TrendsController : ControllerBase
{
    private readonly ISender _sender;

    public TrendsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Retrieves the top trending hashtags calculated using a 24-hour sliding window with exponential time decay.
    /// Served entirely from high-performance Redis cache without querying PostgreSQL.
    /// </summary>
    [HttpGet("api/v1/trends")]
    [ProducesResponseType(typeof(ApiResponse<TrendingTopicsResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTrends(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var query = new GetTrendingTopicsQuery(limit);
            var result = await _sender.Send(query, cancellationToken);
            sw.Stop();
            TrendingMetrics.ApiDuration.Record(sw.Elapsed.TotalMilliseconds);

            return Ok(ApiResponse<TrendingTopicsResult>.Ok(result));
        }
        catch (Exception)
        {
            sw.Stop();
            TrendingMetrics.ApiDuration.Record(sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
