using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Posts.CreatePost;
using CEBAS.Api.Features.Posts.CreateReply;
using CEBAS.Api.Features.Posts.DeletePost;
using CEBAS.Api.Features.Posts.GetFeed;
using CEBAS.Api.Features.Posts.GetPost;
using CEBAS.Api.Features.Posts.GetReplies;
using CEBAS.Api.Features.Posts.GetUserPosts;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Domain.Exceptions;

using CEBAS.Api.Features.Posts.GetUserReplies;

namespace CEBAS.Api.Controllers;

[ApiController]
public class PostsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public PostsController(
        ISender sender,
        ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves timeline / feed posts for the home feed with keyset cursor pagination.
    /// </summary>
    [HttpGet("api/v1/posts")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<PostResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeed(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFeedQuery(_currentUser.UserId, cursor, limit);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<PostResponse>>.Ok(result));
    }

    /// <summary>
    /// Retrieves posts published by a specific user by username for profile tabs with cursor pagination.
    /// </summary>
    [HttpGet("api/v1/users/{username}/posts")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<PostResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserPosts(
        [FromRoute] string username,
        [FromQuery] string? filter = "posts",
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserPostsQuery(username, _currentUser.UserId, filter, cursor, limit);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<PostResponse>>.Ok(result));
    }

    /// <summary>
    /// Retrieves replies authored by a specific user by username for the profile replies tab with cursor pagination.
    /// </summary>
    [HttpGet("api/v1/users/{username}/replies")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<UserReplyResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserReplies(
        [FromRoute] string username,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserRepliesQuery(username, _currentUser.UserId, cursor, limit);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<UserReplyResponse>>.Ok(result));
    }

    /// <summary>
    /// Creates a new short-form post with optional text and up to 4 media image attachments.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPost("api/v1/posts")]
    [ProducesResponseType(typeof(ApiResponse<PostResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to create posts.");
        }

        var command = new CreatePostCommand(
            _currentUser.UserId.Value,
            request.Content,
            request.MediaIds
        );

        var result = await _sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PostResponse>.Ok(result, "Post created successfully."));
    }

    /// <summary>
    /// Retrieves full post details by stable identifier including author metadata and attached media.
    /// </summary>
    [HttpGet("api/v1/posts/{id}")]
    [ProducesResponseType(typeof(ApiResponse<PostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPost([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var query = new GetPostQuery(id, _currentUser.UserId);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<PostResponse>.Ok(result));
    }

    /// <summary>
    /// Soft-deletes a post by ID. Only the post author is authorized to perform deletion.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpDelete("api/v1/posts/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePost([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to delete posts.");
        }

        var command = new DeletePostCommand(id, _currentUser.UserId.Value);
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Post deleted successfully."));
    }

    /// <summary>
    /// Creates a direct or nested reply to a post.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPost("api/v1/posts/{id}/replies")]
    [ProducesResponseType(typeof(ApiResponse<ReplyResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReply(
        [FromRoute] Guid id,
        [FromBody] CreateReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to create replies.");
        }

        var command = new CreateReplyCommand(
            id,
            _currentUser.UserId.Value,
            request.Content,
            request.ParentReplyId
        );

        var result = await _sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ReplyResponse>.Ok(result, "Reply created successfully."));
    }

    /// <summary>
    /// Retrieves a deterministic hierarchical tree of replies for a post with cursor pagination.
    /// </summary>
    [HttpGet("api/v1/posts/{id}/replies")]
    [ProducesResponseType(typeof(ApiResponse<HierarchicalRepliesResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReplies(
        [FromRoute] Guid id,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetRepliesQuery(id, _currentUser.UserId, cursor, limit);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<HierarchicalRepliesResult>.Ok(result));
    }
}
