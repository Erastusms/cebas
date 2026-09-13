namespace CEBAS.Application.Contracts.Trending;

/// <summary>
/// DTO representing a single ranked trending topic.
/// </summary>
public sealed record TrendingTopicDto(
    int Rank,
    string Tag,
    double Score,
    long PostCount
);

/// <summary>
/// Result envelope containing the top trending topics.
/// </summary>
public sealed record TrendingTopicsResult(
    IReadOnlyList<TrendingTopicDto> Items
);
