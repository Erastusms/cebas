using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Timelines.Home;
using CEBAS.Api.Features.Timelines.Users.GetUserLikes;
using CEBAS.Api.Features.Timelines.Users.GetUserPosts;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
public class TimelinesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public TimelinesController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves chronological home feed containing posts from followed users and authenticated viewer.
    /// Utilizes keyset/cursor pagination with deterministic ordering (created_at DESC, id DESC).
    /// Enforces dynamic bidirectional block filtering server-side.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpGet("api/v1/timelines/home")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<PostResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHomeTimeline(
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to access home timeline.");
        }

        var query = new GetHomeTimelineQuery(_currentUser.UserId.Value, cursor, limit);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<PostResponse>>.Ok(result));
    }

    /// <summary>
    /// Retrieves posts published by a specific user (by UUID or username) with keyset cursor pagination.
    /// </summary>
    [HttpGet("api/v1/users/{id}/posts")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<PostResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserPosts(
        [FromRoute] string id,
        [FromQuery] string? filter = "posts",
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserPostsTimelineQuery(id, _currentUser.UserId, filter, cursor, limit);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<PostResponse>>.Ok(result));
    }

    /// <summary>
    /// Retrieves posts liked by a specific user (by UUID or username) ordered by like creation time with keyset cursor pagination.
    /// Excludes blocked authors and returns viewer-specific engagement state.
    /// </summary>
    [HttpGet("api/v1/users/{id}/likes")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<PostResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserLikes(
        [FromRoute] string id,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserLikesTimelineQuery(id, _currentUser.UserId, cursor, limit);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<PostResponse>>.Ok(result));
    }
}
