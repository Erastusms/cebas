using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Notifications;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Notifications.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<MarkAllNotificationsReadResponse>;

public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, MarkAllNotificationsReadResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MarkAllNotificationsReadCommandHandler> _logger;

    public MarkAllNotificationsReadCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<MarkAllNotificationsReadCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MarkAllNotificationsReadResponse> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var unreadNotifications = await _dbContext.Notifications
            .Where(n => n.RecipientId == request.UserId && !n.IsRead)
            .ToListAsync(cancellationToken);

        int count = unreadNotifications.Count;
        if (count > 0)
        {
            foreach (var notification in unreadNotifications)
            {
                notification.MarkAsRead(now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", count, request.UserId);
        }

        return new MarkAllNotificationsReadResponse(count);
    }
}
