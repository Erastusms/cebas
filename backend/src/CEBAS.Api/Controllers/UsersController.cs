using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Users.GetCurrentUser;
using CEBAS.Api.Features.Users.GetPublicProfile;
using CEBAS.Api.Features.Users.GetSessions;
using CEBAS.Api.Features.Users.RevokeSession;
using CEBAS.Api.Features.Users.UpdateProfile;
using CEBAS.Api.Services;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Media;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;
    private readonly ICookieService _cookieService;

    public UsersController(
        ISender sender,
        ICurrentUser currentUser,
        ICookieService cookieService)
    {
        _sender = sender;
        _currentUser = currentUser;
        _cookieService = cookieService;
    }

    /// <summary>
    /// Retrieves the public profile of a user by canonical username (case-insensitive).
    /// </summary>
    [HttpGet("api/v1/users/{username}")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile([FromRoute] string username, CancellationToken cancellationToken)
    {
        var profile = await _sender.Send(new GetPublicProfileQuery(username), cancellationToken);
        return Ok(ApiResponse<UserProfileResponse>.Ok(profile));
    }

    /// <summary>
    /// Retrieves current authenticated user's profile and account information.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpGet("api/v1/users/me")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var user = await _sender.Send(new GetCurrentUserQuery(_currentUser.UserId.Value), cancellationToken);
        return Ok(ApiResponse<CurrentUserResponse>.Ok(user));
    }

    /// <summary>
    /// Updates current authenticated user's display name and biography.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPatch("api/v1/users/me")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var command = new UpdateProfileCommand(_currentUser.UserId.Value, request.DisplayName, request.Bio);
        var updatedUser = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<CurrentUserResponse>.Ok(updatedUser, "Profile updated successfully."));
    }

    /// <summary>
    /// Updates current authenticated user's avatar referencing a confirmed, ready media record.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPut("api/v1/users/me/avatar")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var command = new Features.Users.UpdateAvatar.UpdateAvatarCommand(_currentUser.UserId.Value, request.MediaId);
        var updatedUser = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<CurrentUserResponse>.Ok(updatedUser, "Avatar updated successfully."));
    }

    /// <summary>
    /// Lists all active login sessions/devices for the current authenticated user.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpGet("api/v1/users/me/sessions")]
    [ProducesResponseType(typeof(ApiResponse<List<SessionItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserSessions(CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var sessions = await _sender.Send(new GetSessionsQuery(_currentUser.UserId.Value, _currentUser.SessionId), cancellationToken);
        return Ok(ApiResponse<List<SessionItemResponse>>.Ok(sessions));
    }

    /// <summary>
    /// Revokes a specific session belonging to the current authenticated user.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpDelete("api/v1/users/me/sessions/{sessionId}")]
    [HttpPost("api/v1/users/me/sessions/{sessionId}/revoke")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        await _sender.Send(new RevokeSessionCommand(_currentUser.UserId.Value, sessionId), cancellationToken);

        // If user revokes their own current session, clear cookie immediately
        if (_currentUser.SessionId.HasValue && _currentUser.SessionId.Value == sessionId)
        {
            _cookieService.ClearSessionCookie(Response);
        }

        return Ok(ApiResponse<object>.Ok(new { }, "Session revoked successfully."));
    }
}
