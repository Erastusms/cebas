using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Safety.Moderation.ExecuteModerationAction;
using CEBAS.Api.Features.Safety.Moderation.GetReports;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Reports;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName, Policy = "ModeratorOrAdmin")]
public class AdminReportsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public AdminReportsController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves a paginated list of moderation reports with queue filtering.
    /// Restricted to MODERATOR and ADMIN roles.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedReportsResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReports(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? targetType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = new GetReportsQuery(status, category, targetType, page, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<PagedReportsResult>.Ok(result));
    }

    /// <summary>
    /// Executes a controlled moderation action (RESOLVE, DISMISS, HIDE_POST, SUSPEND_USER) on a report.
    /// Restricted to MODERATOR and ADMIN roles.
    /// </summary>
    [HttpPost("{id:guid}/action")]
    [ProducesResponseType(typeof(ApiResponse<ModerationActionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExecuteAction(
        [FromRoute] Guid id,
        [FromBody] ModerationActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated moderator context is required.");
        }

        var command = new ExecuteModerationActionCommand(
            id,
            _currentUser.UserId.Value,
            request.Action,
            request.Reason
        );

        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<ModerationActionResponse>.Ok(result, result.Message));
    }
}
