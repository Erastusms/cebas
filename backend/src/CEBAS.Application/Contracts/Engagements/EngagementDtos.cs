namespace CEBAS.Application.Contracts.Engagements;

/// <summary>
/// Response contract for Like mutation operations.
/// Provides authoritative server state for frontend reconciliation.
/// </summary>
public record LikeResponse(
    Guid PostId,
    bool Liked,
    int LikeCount
);

/// <summary>
/// Response contract for Bookmark mutation operations.
/// Provides authoritative server state for frontend reconciliation.
/// </summary>
public record BookmarkResponse(
    Guid PostId,
    bool Bookmarked,
    int BookmarkCount
);

/// <summary>
/// Response contract for bookmarked post items in the bookmarks list page.
/// </summary>
public record BookmarkedPostResponse(
    Guid BookmarkId,
    DateTimeOffset BookmarkedAt,
    Guid PostId,
    string Content,
    CEBAS.Application.Contracts.Posts.PostAuthorDto Author,
    List<CEBAS.Application.Contracts.Posts.PostMediaDto> Media,
    int ReplyCount,
    int MediaCount,
    int LikeCount,
    int BookmarkCount,
    bool Liked,
    bool Bookmarked,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
