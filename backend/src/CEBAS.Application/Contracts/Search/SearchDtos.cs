using System.Text.Json.Serialization;
using CEBAS.Application.Contracts.Posts;

namespace CEBAS.Application.Contracts.Search;

public record SearchPostsQuery(
    string? Query,
    string? Cursor = null,
    int Limit = 20,
    Guid? CurrentUserId = null
);

public record SearchUsersQuery(
    string? Query,
    int Limit = 20,
    Guid? CurrentUserId = null
);

public record SearchSummaryQuery(
    string? Query,
    int PostLimit = 5,
    int UserLimit = 5,
    Guid? CurrentUserId = null
);

public record SearchPostItemDto(
    Guid Id,
    string Content,
    string? HighlightedContent,
    PostAuthorDto Author,
    List<PostMediaDto> Media,
    int ReplyCount,
    int MediaCount,
    int LikeCount,
    int BookmarkCount,
    bool Liked,
    bool Bookmarked,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    double? Score = null
)
{
    [JsonPropertyName("has_liked")]
    public bool HasLikedSnake => Liked;

    [JsonPropertyName("has_bookmarked")]
    public bool HasBookmarkedSnake => Bookmarked;

    public bool HasLiked => Liked;
    public bool HasBookmarked => Bookmarked;
}

public record SearchUserItemDto(
    Guid Id,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    bool IsVerified,
    string? HighlightedUsername = null,
    string? HighlightedDisplayName = null,
    string? HighlightedBio = null,
    bool IsFollowing = false,
    double? Score = null
);

public record SearchSummaryResponse(
    IReadOnlyList<SearchPostItemDto> Posts,
    IReadOnlyList<SearchUserItemDto> Users
);

public record SearchHealthStatus(
    string Status,
    bool Available,
    string ClusterName,
    long PostCount,
    long UserCount
);

public record ReindexSummary(
    int ProcessedPosts,
    int ProcessedUsers,
    int Errors,
    double ElapsedMilliseconds
);
