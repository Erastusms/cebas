using MediatR;
using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Users.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<CurrentUserResponse>;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    private readonly ApplicationDbContext _dbContext;

    public GetCurrentUserQueryHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CurrentUserResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedException("User profile could not be resolved from active session.");
        }

        return new CurrentUserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.BannerUrl,
            user.Role.ToString().ToUpperInvariant(),
            user.IsVerified,
            user.CreatedAt,
            user.UpdatedAt
        );
    }
}
