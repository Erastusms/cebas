using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Engagements.Bookmarks.CreateBookmark;
using CEBAS.Api.Features.Engagements.Bookmarks.GetBookmarks;
using CEBAS.Api.Features.Engagements.Bookmarks.RemoveBookmark;
using CEBAS.Api.Features.Engagements.Likes.CreateLike;
using CEBAS.Api.Features.Engagements.Likes.RemoveLike;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Engagements;
using CEBAS.Domain.Exceptions;

using Microsoft.AspNetCore.RateLimiting;
using CEBAS.Api.RateLimiting;

namespace CEBAS.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingRegistration.EngagementPolicy)]
public class EngagementsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public EngagementsController(
        ISender sender,
        ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Creates a Like on a post for the authenticated user.
    /// Operation is idempotent and safe under concurrent requests.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPost("api/v1/posts/{id}/likes")]
    [ProducesResponseType(typeof(ApiResponse<LikeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLike([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to like posts.");
        }

        var command = new CreateLikeCommand(_currentUser.UserId.Value, id);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<LikeResponse>.Ok(result, "Post liked successfully."));
    }

    /// <summary>
    /// Removes a Like on a post for the authenticated user.
    /// Operation is idempotent and only decrements counters when a like actually existed.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpDelete("api/v1/posts/{id}/likes")]
    [ProducesResponseType(typeof(ApiResponse<LikeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLike([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to unlike posts.");
        }

        var command = new RemoveLikeCommand(_currentUser.UserId.Value, id);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<LikeResponse>.Ok(result, "Post unliked successfully."));
    }

    /// <summary>
    /// Creates a Bookmark on a post for the authenticated user.
    /// Operation is idempotent and safe under concurrent requests.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPost("api/v1/posts/{id}/bookmarks")]
    [ProducesResponseType(typeof(ApiResponse<BookmarkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateBookmark([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to bookmark posts.");
        }

        var command = new CreateBookmarkCommand(_currentUser.UserId.Value, id);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<BookmarkResponse>.Ok(result, "Post bookmarked successfully."));
    }

    /// <summary>
    /// Removes a Bookmark on a post for the authenticated user.
    /// Operation is idempotent and only decrements counters when a bookmark actually existed.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpDelete("api/v1/posts/{id}/bookmarks")]
    [ProducesResponseType(typeof(ApiResponse<BookmarkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBookmark([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to remove bookmarks.");
        }

        var command = new RemoveBookmarkCommand(_currentUser.UserId.Value, id);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<BookmarkResponse>.Ok(result, "Bookmark removed successfully."));
    }

    /// <summary>
    /// Retrieves bookmarks belonging to the authenticated user using keyset cursor pagination.
    /// Private saved-state endpoint: never accepts foreign user IDs.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpGet("api/v1/bookmarks")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<BookmarkedPostResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBookmarks(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to view bookmarks.");
        }

        var query = new GetBookmarksQuery(_currentUser.UserId.Value, cursor, limit);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<BookmarkedPostResponse>>.Ok(result));
    }
}
