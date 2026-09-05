using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Notifications;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Notifications.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid UserId, Guid NotificationId) : IRequest<MarkNotificationReadResponse>;

public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, MarkNotificationReadResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MarkNotificationReadCommandHandler> _logger;

    public MarkNotificationReadCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<MarkNotificationReadCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MarkNotificationReadResponse> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken);

        if (notification == null)
        {
            throw new NotFoundException($"Notification with ID '{request.NotificationId}' was not found.");
        }

        // Server-side security check: user can only mark their own notifications as read
        if (notification.RecipientId != request.UserId)
        {
            _logger.LogWarning("Security violation: User {UserId} attempted to mark notification {NotificationId} owned by {OwnerId}",
                request.UserId, notification.Id, notification.RecipientId);
            throw new ForbiddenException("Cannot modify notifications belonging to another user.");
        }

        // Idempotent operation
        if (!notification.IsRead)
        {
            notification.MarkAsRead(DateTimeOffset.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Notification {NotificationId} marked as read by user {UserId}",
                notification.Id, request.UserId);
        }

        return new MarkNotificationReadResponse(notification.Id, notification.IsRead, notification.ReadAt);
    }
}
