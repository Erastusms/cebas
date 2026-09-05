using MediatR;
using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Contracts.Reports;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Safety.Moderation.GetReports;

public sealed record GetReportsQuery(
    string? Status = null,
    string? Category = null,
    string? TargetType = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<PagedReportsResult>;

public sealed class GetReportsQueryHandler : IRequestHandler<GetReportsQuery, PagedReportsResult>
{
    private readonly ApplicationDbContext _dbContext;

    public GetReportsQueryHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedReportsResult> Handle(GetReportsQuery request, CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.Reports.AsNoTracking().AsQueryable();

        // 1. Status Filter
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ReportStatus>(request.Status, true, out var status))
        {
            baseQuery = baseQuery.Where(r => r.Status == status);
        }

        // 2. Category Filter
        if (!string.IsNullOrWhiteSpace(request.Category) && Enum.TryParse<ReportCategory>(request.Category, true, out var category))
        {
            baseQuery = baseQuery.Where(r => r.Category == category);
        }

        // 3. Target Type Filter
        if (!string.IsNullOrWhiteSpace(request.TargetType))
        {
            if (request.TargetType.Equals("Post", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(r => r.TargetPostId != null);
            }
            else if (request.TargetType.Equals("User", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(r => r.TargetUserId != null);
            }
        }

        // 4. Group by target (TargetPostId and TargetUserId)
        var targetGroupsQuery = baseQuery
            .GroupBy(r => new { r.TargetPostId, r.TargetUserId })
            .Select(g => new
            {
                g.Key.TargetPostId,
                g.Key.TargetUserId,
                ReportCount = g.Count(),
                LatestCreatedAt = g.Max(r => r.CreatedAt),
                HasPending = g.Any(r => r.Status == ReportStatus.PENDING)
            })
            .OrderBy(g => g.HasPending ? 0 : 1)
            .ThenByDescending(g => g.LatestCreatedAt);

        // 5. Pagination on target stacks
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        int totalCount = await targetGroupsQuery.CountAsync(cancellationToken);
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        if (totalCount == 0)
        {
            return new PagedReportsResult(new List<ModerationReportItemResponse>(), page, pageSize, 0, 0);
        }

        var pagedTargets = await targetGroupsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var postIds = pagedTargets.Where(t => t.TargetPostId.HasValue).Select(t => t.TargetPostId!.Value).ToList();
        var userIds = pagedTargets.Where(t => t.TargetUserId.HasValue).Select(t => t.TargetUserId!.Value).ToList();

        // 6. Hydrate reports and target context for these paged stacks
        var reportsQuery = _dbContext.Reports
            .AsNoTracking()
            .Include(r => r.Reporter)
            .Include(r => r.TargetUser)
            .Include(r => r.TargetPost!)
                .ThenInclude(p => p.Author)
            .Include(r => r.TargetPost!)
                .ThenInclude(p => p.MediaAttachments)
            .AsQueryable();

        if (postIds.Count > 0 && userIds.Count > 0)
        {
            reportsQuery = reportsQuery.Where(r =>
                (r.TargetPostId != null && postIds.Contains(r.TargetPostId.Value)) ||
                (r.TargetUserId != null && userIds.Contains(r.TargetUserId.Value)));
        }
        else if (postIds.Count > 0)
        {
            reportsQuery = reportsQuery.Where(r => r.TargetPostId != null && postIds.Contains(r.TargetPostId.Value));
        }
        else if (userIds.Count > 0)
        {
            reportsQuery = reportsQuery.Where(r => r.TargetUserId != null && userIds.Contains(r.TargetUserId.Value));
        }

        var reportsForTargets = await reportsQuery
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = new List<ModerationReportItemResponse>();

        foreach (var target in pagedTargets)
        {
            var targetReports = reportsForTargets
                .Where(r => (target.TargetPostId.HasValue && r.TargetPostId == target.TargetPostId.Value) ||
                            (target.TargetUserId.HasValue && r.TargetUserId == target.TargetUserId.Value))
                .OrderBy(r => r.Status == ReportStatus.PENDING ? 0 : 1)
                .ThenBy(r => r.CreatedAt) // Keep original/initial report so new reports don't overwrite the card
                .ToList();

            if (targetReports.Count == 0)
            {
                continue;
            }

            var primaryReport = targetReports.First();

            ReportedPostPreview? postPreview = null;
            var targetPostEntity = targetReports.Select(r => r.TargetPost).FirstOrDefault(p => p != null);
            if (targetPostEntity != null)
            {
                var mediaUrls = targetPostEntity.MediaAttachments
                    .OrderBy(m => m.Position)
                    .Select(m => $"/api/v1/media/{m.MediaId}")
                    .ToList();

                postPreview = new ReportedPostPreview(
                    targetPostEntity.Id,
                    targetPostEntity.AuthorId,
                    targetPostEntity.Author?.Username ?? "unknown",
                    targetPostEntity.Author?.DisplayName ?? "Unknown User",
                    targetPostEntity.Author?.AvatarUrl,
                    targetPostEntity.Content,
                    mediaUrls,
                    targetPostEntity.CreatedAt,
                    targetPostEntity.IsDeleted,
                    targetPostEntity.IsHidden
                );
            }

            ReportedUserPreview? userPreview = null;
            var targetUserEntity = targetReports.Select(r => r.TargetUser ?? r.TargetPost?.Author).FirstOrDefault(u => u != null);
            if (targetUserEntity != null)
            {
                userPreview = new ReportedUserPreview(
                    targetUserEntity.Id,
                    targetUserEntity.Username,
                    targetUserEntity.DisplayName,
                    targetUserEntity.AvatarUrl,
                    targetUserEntity.Role.ToString(),
                    targetUserEntity.IsSuspended,
                    targetUserEntity.CreatedAt
                );
            }

            var reportDetails = targetReports.Select(r => new ReportDetailItem(
                r.Id,
                r.ReporterUserId,
                r.Reporter?.Username ?? "unknown",
                r.Reporter?.DisplayName ?? "Unknown User",
                r.Reporter?.AvatarUrl,
                r.Category.ToString(),
                r.Status.ToString(),
                r.Reason,
                r.CreatedAt,
                r.ResolvedAt,
                r.ResolvedByUserId
            )).ToList();

            var categories = targetReports
                .Select(r => r.Category.ToString())
                .Distinct()
                .ToList();

            bool hasPending = targetReports.Any(r => r.Status == ReportStatus.PENDING);
            string overallStatus = hasPending ? "PENDING" : primaryReport.Status.ToString();

            items.Add(new ModerationReportItemResponse(
                primaryReport.Id,
                primaryReport.ReporterUserId,
                primaryReport.Reporter?.Username ?? "unknown",
                primaryReport.Reporter?.DisplayName ?? "Unknown User",
                primaryReport.Reporter?.AvatarUrl,
                target.TargetPostId.HasValue ? "Post" : "User",
                target.TargetPostId,
                target.TargetUserId,
                primaryReport.Category.ToString(),
                overallStatus,
                primaryReport.Reason,
                primaryReport.CreatedAt,
                primaryReport.ResolvedAt,
                primaryReport.ResolvedByUserId,
                postPreview,
                userPreview,
                ReportCount: target.ReportCount > 0 ? target.ReportCount : targetReports.Count,
                Categories: categories,
                Reports: reportDetails
            ));
        }

        return new PagedReportsResult(items, page, pageSize, totalCount, totalPages);
    }
}
