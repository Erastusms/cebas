namespace CEBAS.Application.Contracts.Reports;

public sealed record CreateReportRequest(
    Guid? TargetPostId,
    Guid? TargetUserId,
    string Category,
    string? Description
);

public sealed record ReportResponse(
    Guid Id,
    Guid ReporterUserId,
    Guid? TargetPostId,
    Guid? TargetUserId,
    string Category,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedByUserId
);

public sealed record ReportedPostPreview(
    Guid Id,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string Content,
    IReadOnlyList<string> MediaUrls,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    bool IsHidden
);

public sealed record ReportedUserPreview(
    Guid Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    bool IsSuspended,
    DateTimeOffset CreatedAt
);

public sealed record ReportDetailItem(
    Guid Id,
    Guid ReporterUserId,
    string ReporterUsername,
    string ReporterDisplayName,
    string? ReporterAvatarUrl,
    string Category,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedByUserId
);

public sealed record ModerationReportItemResponse(
    Guid Id,
    Guid ReporterUserId,
    string ReporterUsername,
    string ReporterDisplayName,
    string? ReporterAvatarUrl,
    string TargetType,
    Guid? TargetPostId,
    Guid? TargetUserId,
    string Category,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedByUserId,
    ReportedPostPreview? TargetPost,
    ReportedUserPreview? TargetUser,
    int ReportCount = 1,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<ReportDetailItem>? Reports = null
);

public sealed record ModerationActionRequest(
    string Action,
    string? Reason
);

public sealed record ModerationActionResponse(
    Guid ReportId,
    string Action,
    string Status,
    string Message,
    DateTimeOffset Timestamp
);

public sealed record PagedReportsResult(
    IReadOnlyList<ModerationReportItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

public sealed record SuspendedUserItemResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    DateTimeOffset? SuspendedAt,
    string? SuspensionReason,
    int TotalPosts,
    DateTimeOffset CreatedAt
);

public sealed record PagedSuspendedUsersResult(
    IReadOnlyList<SuspendedUserItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

public sealed record UnsuspendUserRequest(
    string? Reason
);

public sealed record UnsuspendUserResponse(
    Guid UserId,
    string Username,
    string Message,
    DateTimeOffset Timestamp
);

