using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Events;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Auth.Logout;

public sealed record LogoutCommand(string? RawSessionToken) : IRequest<bool>;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISessionTokenService _tokenService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        ApplicationDbContext dbContext,
        ISessionTokenService tokenService,
        ILogger<LogoutCommandHandler> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawSessionToken))
        {
            return true;
        }

        var tokenHash = _tokenService.ComputeTokenHash(request.RawSessionToken);
        var session = await _dbContext.Sessions
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

        if (session != null && session.RevokedAt == null)
        {
            session.Revoke(DateTimeOffset.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Session revoked on logout [SessionId: {SessionId}]", session.Id);
        }

        return true;
    }
}

public sealed class UserLoggedOutEventHandler : INotificationHandler<UserLoggedOutDomainEvent>
{
    private readonly ILogger<UserLoggedOutEventHandler> _logger;

    public UserLoggedOutEventHandler(ILogger<UserLoggedOutEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserLoggedOutDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DomainEvent] UserLoggedOut: UserId {UserId}, SessionId {SessionId} at {OccurredAt}",
            notification.UserId, notification.SessionId, notification.OccurredAt);
        return Task.CompletedTask;
    }
}
