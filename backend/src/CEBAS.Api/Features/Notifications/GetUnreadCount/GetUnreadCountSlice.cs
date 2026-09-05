using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Notifications;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Notifications.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid UserId) : IRequest<UnreadNotificationCountResponse>;

public sealed class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, UnreadNotificationCountResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GetUnreadCountQueryHandler> _logger;

    public GetUnreadCountQueryHandler(
        ApplicationDbContext dbContext,
        ILogger<GetUnreadCountQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UnreadNotificationCountResponse> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var blockedUserIds = await _dbContext.Blocks
            .AsNoTracking()
            .Where(b => b.BlockerId == request.UserId || b.BlockedId == request.UserId)
            .Select(b => b.BlockerId == request.UserId ? b.BlockedId : b.BlockerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == request.UserId && !n.IsRead);

        if (blockedUserIds.Count > 0)
        {
            query = query.Where(n => !blockedUserIds.Contains(n.ActorId));
        }

        var count = await query.CountAsync(cancellationToken);
        return new UnreadNotificationCountResponse(count);
    }
}
