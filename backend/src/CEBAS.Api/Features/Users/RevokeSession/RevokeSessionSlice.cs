using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Domain.Events;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Users.RevokeSession;

public sealed record RevokeSessionCommand(
    Guid UserId,
    Guid SessionId
) : IRequest<bool>;

public sealed class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, bool>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RevokeSessionCommandHandler> _logger;

    public RevokeSessionCommandHandler(
        ApplicationDbContext dbContext,
        ILogger<RevokeSessionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            throw new NotFoundException("Session not found.");
        }

        if (session.UserId != request.UserId)
        {
            throw new ForbiddenException("You cannot revoke another user's session.");
        }

        if (session.RevokedAt == null)
        {
            session.Revoke(DateTimeOffset.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Session {SessionId} revoked for UserId {UserId}", request.SessionId, request.UserId);
        }

        return true;
    }
}

public sealed class SessionRevokedEventHandler : INotificationHandler<SessionRevokedDomainEvent>
{
    private readonly ILogger<SessionRevokedEventHandler> _logger;

    public SessionRevokedEventHandler(ILogger<SessionRevokedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SessionRevokedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] SessionRevoked: UserId {UserId}, SessionId {SessionId} at {OccurredAt}",
            notification.UserId, notification.SessionId, notification.OccurredAt);
        return Task.CompletedTask;
    }
}
