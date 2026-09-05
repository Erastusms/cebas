using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Notifications.GetNotifications;
using CEBAS.Api.Features.Notifications.GetUnreadCount;
using CEBAS.Api.Features.Notifications.MarkAllNotificationsRead;
using CEBAS.Api.Features.Notifications.MarkNotificationRead;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Notifications;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
public class NotificationsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(
        ISender sender,
        ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves paginated notifications for the authenticated user using keyset cursor pagination.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpGet("api/v1/notifications")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<NotificationResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        [FromQuery(Name = "unread_only")] bool? unreadOnlySnake = null,
        [FromQuery(Name = "unreadOnly")] bool? unreadOnlyCamel = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to view notifications.");
        }

        bool unreadOnly = unreadOnlySnake ?? unreadOnlyCamel ?? false;
        var query = new GetNotificationsQuery(_currentUser.UserId.Value, cursor, limit, unreadOnly);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<CursorPagedResult<NotificationResponseDto>>.Ok(result, "Notifications retrieved successfully."));
    }

    /// <summary>
    /// Retrieves the count of unread notifications for the authenticated user.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpGet("api/v1/notifications/unread-count")]
    [ProducesResponseType(typeof(ApiResponse<UnreadNotificationCountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to view unread notification count.");
        }

        var query = new GetUnreadCountQuery(_currentUser.UserId.Value);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(ApiResponse<UnreadNotificationCountResponse>.Ok(result, "Unread notification count retrieved."));
    }

    /// <summary>
    /// Marks a single notification as read.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPatch("api/v1/notifications/{id}/read")]
    [ProducesResponseType(typeof(ApiResponse<MarkNotificationReadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to mark notifications as read.");
        }

        var command = new MarkNotificationReadCommand(_currentUser.UserId.Value, id);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<MarkNotificationReadResponse>.Ok(result, "Notification marked as read."));
    }

    /// <summary>
    /// Marks all unread notifications as read for the authenticated user.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPatch("api/v1/notifications/read-all")]
    [ProducesResponseType(typeof(ApiResponse<MarkAllNotificationsReadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to mark notifications as read.");
        }

        var command = new MarkAllNotificationsReadCommand(_currentUser.UserId.Value);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<MarkAllNotificationsReadResponse>.Ok(result, "All notifications marked as read."));
    }
}
