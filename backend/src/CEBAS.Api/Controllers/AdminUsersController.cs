using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Safety.Moderation.GetSuspendedUsers;
using CEBAS.Api.Features.Safety.Moderation.UnsuspendUser;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Reports;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName, Policy = "ModeratorOrAdmin")]
public class AdminUsersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public AdminUsersController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves a paginated list of suspended users with search and pagination.
    /// Restricted to MODERATOR and ADMIN roles.
    /// </summary>
    [HttpGet("suspended")]
    [ProducesResponseType(typeof(ApiResponse<PagedSuspendedUsersResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSuspendedUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSuspendedUsersQuery(page, pageSize, search);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<PagedSuspendedUsersResult>.Ok(result));
    }

    /// <summary>
    /// Unsuspends a previously suspended user, reinstating their account and restoring hidden posts.
    /// Restricted to MODERATOR and ADMIN roles.
    /// </summary>
    [HttpPost("{id:guid}/unsuspend")]
    [ProducesResponseType(typeof(ApiResponse<UnsuspendUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsuspendUser(
        [FromRoute] Guid id,
        [FromBody] UnsuspendUserRequest? request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated staff context is required.");
        }

        var command = new UnsuspendUserCommand(
            id,
            _currentUser.UserId.Value,
            request?.Reason
        );

        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<UnsuspendUserResponse>.Ok(result, result.Message));
    }
}
