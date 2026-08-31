using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.SocialGraph.BlockUser;
using CEBAS.Api.Features.SocialGraph.FollowUser;
using CEBAS.Api.Features.SocialGraph.GetFollowers;
using CEBAS.Api.Features.SocialGraph.GetFollowing;
using CEBAS.Api.Features.SocialGraph.UnblockUser;
using CEBAS.Api.Features.SocialGraph.UnfollowUser;
using CEBAS.Api.Features.Users.GetCurrentUser;
using CEBAS.Api.Features.Users.GetPublicProfile;
using CEBAS.Api.Features.Users.GetSessions;
using CEBAS.Api.Features.Users.RevokeSession;
using CEBAS.Api.Features.Users.UpdateProfile;
using CEBAS.Api.Services;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Media;
using CEBAS.Application.Contracts.SocialGraph;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;
    private readonly ICookieService _cookieService;
    private readonly IUserRepository _userRepository;

    public UsersController(
        ISender sender,
        ICurrentUser currentUser,
        ICookieService cookieService,
        IUserRepository userRepository)
    {
        _sender = sender;
        _currentUser = currentUser;
        _cookieService = cookieService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Helper to resolve target user ID from GUID string or username.
    /// </summary>
    private async Task<Guid> ResolveUserIdAsync(string idOrUsername, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(idOrUsername, out var guid))
        {
            return guid;
        }

        var user = await _userRepository.GetByUsernameAsync(idOrUsername, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException($"User '{idOrUsername}' was not found.");
        }

        return user.Id;
    }

    /// <summary>
    /// Retrieves the public profile of a user by canonical username (case-insensitive).
    /// </summary>
    [HttpGet("api/v1/users/{username}")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile([FromRoute] string username, CancellationToken cancellationToken)
    {
        var profile = await _sender.Send(new GetPublicProfileQuery(username, _currentUser.UserId), cancellationToken);
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

        var command = new UpdateProfileCommand(_currentUser.UserId.Value, request.DisplayName, request.Bio, request.BannerUrl);
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
    /// Updates current authenticated user's background banner referencing media or preset gradient.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPut("api/v1/users/me/banner")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBanner([FromBody] UpdateBannerRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var command = new Features.Users.UpdateBanner.UpdateBannerCommand(_currentUser.UserId.Value, request.MediaId, request.BannerUrl);
        var updatedUser = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<CurrentUserResponse>.Ok(updatedUser, "Banner updated successfully."));
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

    /// <summary>
    /// Follows a target user by ID or canonical username.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPost("api/v1/users/{id}/follow")]
    [ProducesResponseType(typeof(ApiResponse<FollowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FollowUser([FromRoute] string id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var targetUserId = await ResolveUserIdAsync(id, cancellationToken);
        var result = await _sender.Send(new FollowUserCommand(_currentUser.UserId.Value, targetUserId), cancellationToken);
        return Ok(ApiResponse<FollowResponse>.Ok(result, "User followed successfully."));
    }

    /// <summary>
    /// Unfollows a target user by ID or canonical username.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpDelete("api/v1/users/{id}/follow")]
    [ProducesResponseType(typeof(ApiResponse<FollowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnfollowUser([FromRoute] string id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var targetUserId = await ResolveUserIdAsync(id, cancellationToken);
        var result = await _sender.Send(new UnfollowUserCommand(_currentUser.UserId.Value, targetUserId), cancellationToken);
        return Ok(ApiResponse<FollowResponse>.Ok(result, "User unfollowed successfully."));
    }

    /// <summary>
    /// Retrieves a keyset-paginated list of followers for a user.
    /// </summary>
    [HttpGet("api/v1/users/{id}/followers")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<SocialUserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFollowers(
        [FromRoute] string id,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = await ResolveUserIdAsync(id, cancellationToken);
        var result = await _sender.Send(new GetFollowersQuery(targetUserId, _currentUser.UserId, cursor, limit), cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<SocialUserDto>>.Ok(result));
    }

    /// <summary>
    /// Retrieves a keyset-paginated list of accounts followed by a user.
    /// </summary>
    [HttpGet("api/v1/users/{id}/following")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<SocialUserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFollowing(
        [FromRoute] string id,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var targetUserId = await ResolveUserIdAsync(id, cancellationToken);
        var result = await _sender.Send(new GetFollowingQuery(targetUserId, _currentUser.UserId, cursor, limit), cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<SocialUserDto>>.Ok(result));
    }

    /// <summary>
    /// Blocks a target user and removes mutual follow relationships.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPost("api/v1/users/{id}/block")]
    [ProducesResponseType(typeof(ApiResponse<BlockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BlockUser([FromRoute] string id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var targetUserId = await ResolveUserIdAsync(id, cancellationToken);
        var result = await _sender.Send(new BlockUserCommand(_currentUser.UserId.Value, targetUserId), cancellationToken);
        return Ok(ApiResponse<BlockResponse>.Ok(result, "User blocked successfully."));
    }

    /// <summary>
    /// Unblocks a target user.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpDelete("api/v1/users/{id}/block")]
    [ProducesResponseType(typeof(ApiResponse<BlockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnblockUser([FromRoute] string id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is missing or invalid.");
        }

        var targetUserId = await ResolveUserIdAsync(id, cancellationToken);
        var result = await _sender.Send(new UnblockUserCommand(_currentUser.UserId.Value, targetUserId), cancellationToken);
        return Ok(ApiResponse<BlockResponse>.Ok(result, "User unblocked successfully."));
    }
}
