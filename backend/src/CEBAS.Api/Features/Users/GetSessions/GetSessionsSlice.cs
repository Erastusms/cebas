using MediatR;
using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Contracts.Users;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Users.GetSessions;

public sealed record GetSessionsQuery(
    Guid UserId,
    Guid? CurrentSessionId = null
) : IRequest<List<SessionItemResponse>>;

public sealed class GetSessionsQueryHandler : IRequestHandler<GetSessionsQuery, List<SessionItemResponse>>
{
    private readonly ApplicationDbContext _dbContext;

    public GetSessionsQueryHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SessionItemResponse>> Handle(GetSessionsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var sessions = await _dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new SessionItemResponse(
            s.Id,
            s.UserAgent,
            s.IpAddress,
            s.CreatedAt,
            s.ExpiresAt,
            request.CurrentSessionId.HasValue && s.Id == request.CurrentSessionId.Value
        )).ToList();
    }
}
