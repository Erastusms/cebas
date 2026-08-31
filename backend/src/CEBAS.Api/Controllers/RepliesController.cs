using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Posts.DeleteReply;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
public class RepliesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public RepliesController(
        ISender sender,
        ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Soft-deletes a conversation reply by ID. Only the reply author is authorized to perform deletion.
    /// Child replies remain accessible in the conversation hierarchy.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpDelete("api/v1/replies/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReply([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to delete replies.");
        }

        var command = new DeleteReplyCommand(id, _currentUser.UserId.Value);
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Reply deleted successfully."));
    }
}
