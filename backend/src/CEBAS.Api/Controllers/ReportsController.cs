using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Safety.Reports.CreateReport;
using CEBAS.Api.RateLimiting;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Reports;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
public class ReportsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public ReportsController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Creates a user safety report against a post or account.
    /// Rate limited via dedicated Reporting policy.
    /// </summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [EnableRateLimiting(RateLimitingRegistration.ReportingPolicy)]
    [ProducesResponseType(typeof(ApiResponse<ReportResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateReport(
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to create a report.");
        }

        var command = new CreateReportCommand(
            _currentUser.UserId.Value,
            request.TargetPostId,
            request.TargetUserId,
            request.Category,
            request.Description
        );

        var result = await _sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ReportResponse>.Ok(result, "Report submitted successfully."));
    }
}
