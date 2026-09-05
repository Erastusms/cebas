using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Users.GetPublicProfile;

public sealed record GetPublicProfileQuery(string Username, Guid? CurrentUserId = null) : IRequest<UserProfileResponse>;

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

        if (user == null || user.IsSuspended)
        {
            throw new NotFoundException($"User '@{request.Username}' was not found.");
        }

        bool isFollowing = false;
        bool isFollowedBy = false;
        bool isBlocked = false;
        bool isBlockedBy = false;

        if (request.CurrentUserId.HasValue && request.CurrentUserId.Value != user.Id)
        {
            var currentUserId = request.CurrentUserId.Value;

            isFollowing = await _dbContext.Follows
                .AsNoTracking()
                .AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == user.Id, cancellationToken);

            isFollowedBy = await _dbContext.Follows
                .AsNoTracking()
                .AnyAsync(f => f.FollowerId == user.Id && f.FollowingId == currentUserId, cancellationToken);

            isBlocked = await _dbContext.Blocks
                .AsNoTracking()
                .AnyAsync(b => b.BlockerId == currentUserId && b.BlockedId == user.Id, cancellationToken);

            isBlockedBy = await _dbContext.Blocks
                .AsNoTracking()
                .AnyAsync(b => b.BlockerId == user.Id && b.BlockedId == currentUserId, cancellationToken);

            // Server-side block isolation: if target user blocked current user, treat profile as unavailable
            if (isBlockedBy)
            {
                throw new NotFoundException($"User '@{request.Username}' was not found.");
            }
        }

        var followerCount = await _dbContext.Follows
            .AsNoTracking()
            .CountAsync(f => f.FollowingId == user.Id && !f.Follower!.IsSuspended, cancellationToken);

        var followingCount = await _dbContext.Follows
            .AsNoTracking()
            .CountAsync(f => f.FollowerId == user.Id && !f.Following!.IsSuspended, cancellationToken);

        var postCount = await _dbContext.Posts
            .AsNoTracking()
            .CountAsync(p => p.AuthorId == user.Id && !p.IsDeleted && !p.IsHidden, cancellationToken);

        var stats = new UserProfileStats(
            PostCount: postCount,
            FollowerCount: followerCount,
            FollowingCount: followingCount
        );

        var relationship = request.CurrentUserId.HasValue
            ? new UserProfileRelationship(isFollowing, isFollowedBy, isBlocked, isBlockedBy)
            : null;

        return new UserProfileResponse(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.BannerUrl,
            user.IsVerified,
            user.CreatedAt,
            stats,
            relationship
        );
    }
}
