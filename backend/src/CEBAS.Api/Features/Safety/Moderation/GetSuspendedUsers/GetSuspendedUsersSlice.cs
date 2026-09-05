using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Contracts.Reports;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Safety.Moderation.GetSuspendedUsers;

public sealed record GetSuspendedUsersQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null
) : IRequest<PagedSuspendedUsersResult>;

public sealed class GetSuspendedUsersQueryValidator : AbstractValidator<GetSuspendedUsersQuery>
{
    public GetSuspendedUsersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetSuspendedUsersQueryHandler : IRequestHandler<GetSuspendedUsersQuery, PagedSuspendedUsersResult>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GetSuspendedUsersQueryHandler> _logger;

    public GetSuspendedUsersQueryHandler(
        ApplicationDbContext dbContext,
        ILogger<GetSuspendedUsersQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PagedSuspendedUsersResult> Handle(GetSuspendedUsersQuery request, CancellationToken cancellationToken)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsSuspended);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Username.ToLower().Contains(search) ||
                                     u.DisplayName.ToLower().Contains(search));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var users = await query
            .OrderByDescending(u => u.SuspendedAt ?? u.UpdatedAt ?? u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.DisplayName,
                u.AvatarUrl,
                Role = u.Role.ToString(),
                u.SuspendedAt,
                u.SuspensionReason,
                u.CreatedAt,
                TotalPosts = _dbContext.Posts.Count(p => p.AuthorId == u.Id && !p.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new SuspendedUserItemResponse(
            u.Id,
            u.Username,
            u.DisplayName,
            u.AvatarUrl,
            u.Role,
            u.SuspendedAt,
            u.SuspensionReason,
            u.TotalPosts,
            u.CreatedAt
        )).ToList();

        _logger.LogInformation("admin.suspended_users: Retrieved {Count} suspended users (Page {Page}/{TotalPages})",
            items.Count, page, totalPages);

        return new PagedSuspendedUsersResult(
            items.AsReadOnly(),
            page,
            pageSize,
            totalCount,
            totalPages
        );
    }
}
