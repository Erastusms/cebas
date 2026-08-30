using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Users.GetPublicProfile;

public sealed record GetPublicProfileQuery(string Username) : IRequest<UserProfileResponse>;

public sealed class GetPublicProfileQueryValidator : AbstractValidator<GetPublicProfileQuery>
{
    public GetPublicProfileQueryValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username parameter is required.")
            .Length(1, 30).WithMessage("Username parameter cannot exceed 30 characters.");
    }
}

public sealed class GetPublicProfileQueryHandler : IRequestHandler<GetPublicProfileQuery, UserProfileResponse>
{
    private readonly ApplicationDbContext _dbContext;

    public GetPublicProfileQueryHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserProfileResponse> Handle(GetPublicProfileQuery request, CancellationToken cancellationToken)
    {
        var normalized = IdentityNormalizers.NormalizeUsername(request.Username);

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException($"User '@{request.Username}' was not found.");
        }

        // Stats baseline (Phase 1 zero counts, ready for Phase 2 social graph)
        var stats = new UserProfileStats(0, 0, 0);

        return new UserProfileResponse(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.IsVerified,
            user.CreatedAt,
            stats
        );
    }
}
