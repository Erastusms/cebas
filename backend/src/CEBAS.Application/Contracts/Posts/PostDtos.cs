namespace CEBAS.Application.Contracts.Posts;

public record CreatePostRequest(
    string? Content,
    List<Guid>? MediaIds
);

public record PostAuthorDto(
    Guid Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    bool IsVerified
);

public record PostMediaDto(
    Guid Id,
    string Url,
    string? OriginalFileName,
    string? MimeType,
    int Position
);

public record PostResponse(
    Guid Id,
    string Content,
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
    DateTimeOffset? UpdatedAt
)
{
    [System.Text.Json.Serialization.JsonPropertyName("has_liked")]
    public bool HasLikedSnake => Liked;

    [System.Text.Json.Serialization.JsonPropertyName("has_bookmarked")]
    public bool HasBookmarkedSnake => Bookmarked;

    public bool HasLiked => Liked;
    public bool HasBookmarked => Bookmarked;
}

public record CreateReplyRequest(
    string Content,
    Guid? ParentReplyId
);

public record ReplyAuthorDto(
    Guid Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    bool IsVerified
);

public record ReplyResponse(
    Guid Id,
    Guid PostId,
    Guid? ParentReplyId,
    string Content,
    ReplyAuthorDto? Author,
    int Depth,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record HierarchicalRepliesResult(
    IReadOnlyList<ReplyResponse> Items,
    string? NextCursor,
    bool HasNextPage,
    int PageSize
);

public record UserReplyResponse(
    Guid Id,
    Guid PostId,
    Guid? ParentReplyId,
    string Content,
    ReplyAuthorDto Author,
    string? ReplyingToUsername,
    string? ParentPostContent,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
