using System.Diagnostics;
using System.Text.RegularExpressions;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Application.Contracts.Search;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Search.Models;

namespace CEBAS.Infrastructure.Search;

public class SearchServiceException : Exception
{
    public SearchServiceException(string message) : base(message) { }
    public SearchServiceException(string message, Exception inner) : base(message, inner) { }
}

public class ElasticsearchSearchService : ISearchService
{
    private readonly ElasticsearchClient _client;
    private readonly ApplicationDbContext _dbContext;
    private readonly IBlockIsolationService _blockIsolationService;
    private readonly IOptions<ElasticsearchOptions> _options;
    private readonly ILogger<ElasticsearchSearchService> _logger;

    private const int MaxQueryLength = 100;
    private const int MaxLimit = 50;
    private const int DefaultLimit = 20;

    public ElasticsearchSearchService(
        ElasticsearchClient client,
        ApplicationDbContext dbContext,
        IBlockIsolationService blockIsolationService,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchSearchService> logger)
    {
        _client = client;
        _dbContext = dbContext;
        _blockIsolationService = blockIsolationService;
        _options = options;
        _logger = logger;
    }

    public async Task<CursorPagedResult<SearchPostItemDto>> SearchPostsAsync(
        SearchPostsQuery query,
        CancellationToken cancellationToken = default)
    {
        var swTotal = Stopwatch.StartNew();
        SearchMetrics.SearchRequestCount.Add(1);

        var normalizedQuery = NormalizeQuery(query.Query);
        int pageSize = Math.Clamp(query.Limit <= 0 ? DefaultLimit : query.Limit, 1, MaxLimit);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new CursorPagedResult<SearchPostItemDto>
            {
                Items = Array.Empty<SearchPostItemDto>(),
                NextCursor = null,
                HasNextPage = false,
                PageSize = pageSize
            };
        }

        // 1. Decode cursor if provided
        SearchCursor? cursor = null;
        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            if (!SearchCursor.TryDecode(query.Cursor, out cursor, out var cursorErr))
            {
                throw new ValidationException("Cursor", cursorErr ?? "Invalid search cursor.");
            }
        }

        // 2. Fetch bidirectional blocked user IDs for authorization isolation
        HashSet<Guid> blockedUserIds = new();
        if (query.CurrentUserId.HasValue && query.CurrentUserId.Value != Guid.Empty)
        {
            blockedUserIds = await _blockIsolationService.GetBidirectionalBlockedUserIdsAsync(
                query.CurrentUserId.Value, cancellationToken);
        }

        var blockedIdStrings = blockedUserIds.Select(id => id.ToString()).ToList();

        // 3. Build Elasticsearch query
        var boolQuery = BuildPostSearchQuery(normalizedQuery, blockedIdStrings);

        // 4. Execute search against Elasticsearch (with graceful degradation to PostgreSQL)
        SearchResponse<PostDocument>? response = null;
        bool esSuccess = false;
        var esSw = Stopwatch.StartNew();

        try
        {
            response = await _client.SearchAsync<PostDocument>(s =>
            {
                s.Indices(_options.Value.PostsIndex)
                 .Query(boolQuery)
                 .Size(pageSize + 1)
                 .Sort(srt => srt
                     .Score(sc => sc.Order(SortOrder.Desc))
                     .Field(f => f.CreatedAt, fo => fo.Order(SortOrder.Desc))
                     .Field(f => f.Id, fo => fo.Order(SortOrder.Asc))
                 )
                 .Highlight(h => h
                     .PreTags(new[] { "<mark>" })
                     .PostTags(new[] { "</mark>" })
                     .Fields(f => f
                         .Add("content", _ => { })
                     )
                 );

                if (cursor != null)
                {
                    s.SearchAfter(new FieldValue[]
                    {
                        FieldValue.Double(cursor.Score),
                        FieldValue.Long(cursor.CreatedAtMs),
                        FieldValue.String(cursor.Id)
                    });
                }
            }, cancellationToken);
            esSw.Stop();
            SearchMetrics.ElasticsearchLatency.Record(esSw.Elapsed.TotalMilliseconds);

            esSuccess = response != null && response.IsValidResponse;
            if (!esSuccess)
            {
                _logger.LogWarning("Elasticsearch post search returned invalid response: {DebugInfo}. Gracefully degrading to PostgreSQL DB search.", response?.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            SearchMetrics.SearchErrorsCount.Add(1);
            _logger.LogWarning(ex, "Elasticsearch cluster unavailable for post search query: '{Query}'. Gracefully degrading to PostgreSQL DB search.", normalizedQuery);
        }

        if (!esSuccess || response == null)
        {
            return await FallbackSearchPostsFromDbAsync(normalizedQuery, query.Cursor, pageSize, blockedUserIds, query.CurrentUserId, cancellationToken);
        }

        var hits = response.Hits.ToList();

        // 5. Application-layer defensive block filtering
        var eligibleHits = hits
            .Where(h => h.Source != null &&
                        (string.IsNullOrWhiteSpace(h.Source.AuthorId) ||
                         !blockedUserIds.Contains(Guid.TryParse(h.Source.AuthorId, out var aid) ? aid : Guid.Empty)))
            .ToList();

        bool hasNextPage = eligibleHits.Count > pageSize;
        var resultHits = hasNextPage ? eligibleHits.Take(pageSize).ToList() : eligibleHits;

        // 6. Enrich user engagements (liked/bookmarked) if user is authenticated
        var postIds = resultHits
            .Select(h => Guid.TryParse(h.Source?.Id, out var pid) ? pid : Guid.Empty)
            .Where(pid => pid != Guid.Empty)
            .ToList();

        HashSet<Guid> likedPostIds = new();
        HashSet<Guid> bookmarkedPostIds = new();

        if (query.CurrentUserId.HasValue && postIds.Count > 0)
        {
            likedPostIds = (await _dbContext.PostLikes
                .AsNoTracking()
                .Where(pl => pl.UserId == query.CurrentUserId.Value && postIds.Contains(pl.PostId))
                .Select(pl => pl.PostId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            bookmarkedPostIds = (await _dbContext.PostBookmarks
                .AsNoTracking()
                .Where(pb => pb.UserId == query.CurrentUserId.Value && postIds.Contains(pb.PostId))
                .Select(pb => pb.PostId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        // 6b. Enrich post engagement counters & author metadata
        var postMetadata = new Dictionary<Guid, (int ReplyCount, int MediaCount, int LikeCount, int BookmarkCount, string? AvatarUrl, bool IsVerified)>();
        if (postIds.Count > 0)
        {
            var metaList = await _dbContext.Posts
                .AsNoTracking()
                .Where(p => postIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.ReplyCount,
                    p.MediaCount,
                    p.LikeCount,
                    p.BookmarkCount,
                    AvatarUrl = p.Author != null ? p.Author.AvatarUrl : null,
                    IsVerified = p.Author != null && p.Author.IsVerified
                })
                .ToListAsync(cancellationToken);

            postMetadata = metaList.ToDictionary(
                m => m.Id,
                m => (m.ReplyCount, m.MediaCount, m.LikeCount, m.BookmarkCount, m.AvatarUrl, m.IsVerified));
        }

        // 7. Map hits to SearchPostItemDto
        var items = new List<SearchPostItemDto>(resultHits.Count);
        foreach (var hit in resultHits)
        {
            var doc = hit.Source!;
            var postId = Guid.TryParse(doc.Id, out var parsedId) ? parsedId : Guid.NewGuid();
            var authorId = Guid.TryParse(doc.AuthorId, out var parsedAuthorId) ? parsedAuthorId : Guid.Empty;

            string? highlightedContent = null;
            if (hit.Highlight != null && hit.Highlight.TryGetValue("content", out var highlights) && highlights.Count > 0)
            {
                highlightedContent = string.Join(" ... ", highlights);
            }

            postMetadata.TryGetValue(postId, out var meta);

            var authorDto = new PostAuthorDto(
                authorId,
                doc.AuthorUsername,
                doc.AuthorDisplayName,
                meta.AvatarUrl,
                meta.IsVerified
            );

            items.Add(new SearchPostItemDto(
                Id: postId,
                Content: doc.Content,
                HighlightedContent: highlightedContent,
                Author: authorDto,
                Media: new List<PostMediaDto>(),
                ReplyCount: meta.ReplyCount,
                MediaCount: meta.MediaCount,
                LikeCount: meta.LikeCount,
                BookmarkCount: meta.BookmarkCount,
                Liked: likedPostIds.Contains(postId),
                Bookmarked: bookmarkedPostIds.Contains(postId),
                IsDeleted: false,
                CreatedAt: doc.CreatedAt,
                UpdatedAt: doc.UpdatedAt,
                Score: hit.Score
            ));
        }

        // 8. Generate next cursor
        string? nextCursor = null;
        if (hasNextPage && resultHits.Count > 0)
        {
            var lastHit = resultHits[^1];
            var lastDoc = lastHit.Source!;
            var score = lastHit.Score ?? 0;
            var createdAtMs = lastDoc.CreatedAt.ToUnixTimeMilliseconds();
            nextCursor = new SearchCursor(score, createdAtMs, lastDoc.Id).Encode();
        }

        swTotal.Stop();
        SearchMetrics.SearchLatency.Record(swTotal.Elapsed.TotalMilliseconds);
        SearchMetrics.SearchResultsCount.Record(items.Count);

        _logger.LogInformation("SearchPosts: query='{Query}' returned {Count} results (hasNext: {HasNext}) in {Duration}ms",
            normalizedQuery, items.Count, hasNextPage, swTotal.Elapsed.TotalMilliseconds);

        return new CursorPagedResult<SearchPostItemDto>
        {
            Items = items,
            NextCursor = nextCursor,
            HasNextPage = hasNextPage,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<SearchUserItemDto>> SearchUsersAsync(
        SearchUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var swTotal = Stopwatch.StartNew();
        SearchMetrics.SearchRequestCount.Add(1);

        var rawQuery = NormalizeQuery(query.Query);
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return Array.Empty<SearchUserItemDto>();
        }

        // Strip leading @ for handle searches
        var normalizedQuery = rawQuery.StartsWith('@') ? rawQuery[1..].Trim() : rawQuery;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<SearchUserItemDto>();
        }

        int limit = Math.Clamp(query.Limit <= 0 ? DefaultLimit : query.Limit, 1, MaxLimit);

        // Fetch bidirectional blocked user IDs
        HashSet<Guid> blockedUserIds = new();
        if (query.CurrentUserId.HasValue && query.CurrentUserId.Value != Guid.Empty)
        {
            blockedUserIds = await _blockIsolationService.GetBidirectionalBlockedUserIdsAsync(
                query.CurrentUserId.Value, cancellationToken);
        }
        var blockedIdStrings = blockedUserIds.Select(id => id.ToString()).ToList();

        // Build User search query using conceptual ranking
        var boolQuery = BuildUserSearchQuery(normalizedQuery, blockedIdStrings);

        SearchResponse<UserDocument>? response = null;
        bool esSuccess = false;
        var esSw = Stopwatch.StartNew();

        try
        {
            response = await _client.SearchAsync<UserDocument>(s => s
                .Indices(_options.Value.UsersIndex)
                .Query(boolQuery)
                .Size(limit)
                .Highlight(h => h
                    .PreTags(new[] { "<mark>" })
                    .PostTags(new[] { "</mark>" })
                    .Fields(f => f
                        .Add("username", _ => { })
                        .Add("display_name", _ => { })
                        .Add("bio", _ => { })
                    )
                ), cancellationToken);
            esSw.Stop();
            SearchMetrics.ElasticsearchLatency.Record(esSw.Elapsed.TotalMilliseconds);

            esSuccess = response != null && response.IsValidResponse;
            if (!esSuccess)
            {
                _logger.LogWarning("Elasticsearch user search returned invalid response: {DebugInfo}. Gracefully degrading to PostgreSQL DB search.", response?.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            SearchMetrics.SearchErrorsCount.Add(1);
            _logger.LogWarning(ex, "Elasticsearch cluster unavailable for user search query: '{Query}'. Gracefully degrading to PostgreSQL DB search.", normalizedQuery);
        }

        if (!esSuccess || response == null)
        {
            return await FallbackSearchUsersFromDbAsync(normalizedQuery, limit, blockedUserIds, query.CurrentUserId, cancellationToken);
        }

        var hits = response.Hits
            .Where(h => h.Source != null &&
                        (string.IsNullOrWhiteSpace(h.Source.Id) ||
                         !blockedUserIds.Contains(Guid.TryParse(h.Source.Id, out var uid) ? uid : Guid.Empty)))
            .ToList();

        // Check following relationships if current user is authenticated
        var userIds = hits
            .Select(h => Guid.TryParse(h.Source?.Id, out var uid) ? uid : Guid.Empty)
            .Where(uid => uid != Guid.Empty)
            .ToList();

        HashSet<Guid> followingUserIds = new();
        if (query.CurrentUserId.HasValue && userIds.Count > 0)
        {
            followingUserIds = (await _dbContext.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == query.CurrentUserId.Value && userIds.Contains(f.FollowingId))
                .Select(f => f.FollowingId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var items = new List<SearchUserItemDto>(hits.Count);
        foreach (var hit in hits)
        {
            var doc = hit.Source!;
            var userId = Guid.TryParse(doc.Id, out var parsedId) ? parsedId : Guid.NewGuid();

            string? highlightedUsername = null;
            if (hit.Highlight != null && hit.Highlight.TryGetValue("username", out var uHl) && uHl.Count > 0)
            {
                highlightedUsername = uHl.FirstOrDefault();
            }

            string? highlightedDisplayName = null;
            if (hit.Highlight != null && hit.Highlight.TryGetValue("display_name", out var dnHl) && dnHl.Count > 0)
            {
                highlightedDisplayName = dnHl.FirstOrDefault();
            }

            string? highlightedBio = null;
            if (hit.Highlight != null && hit.Highlight.TryGetValue("bio", out var bioHl) && bioHl.Count > 0)
            {
                highlightedBio = string.Join(" ... ", bioHl);
            }

            items.Add(new SearchUserItemDto(
                userId,
                doc.Username,
                doc.DisplayName,
                doc.Bio,
                doc.AvatarUrl,
                doc.IsVerified,
                highlightedUsername,
                highlightedDisplayName,
                highlightedBio,
                followingUserIds.Contains(userId),
                hit.Score
            ));
        }

        swTotal.Stop();
        SearchMetrics.SearchLatency.Record(swTotal.Elapsed.TotalMilliseconds);
        SearchMetrics.SearchResultsCount.Record(items.Count);

        _logger.LogInformation("SearchUsers: query='{Query}' returned {Count} users in {Duration}ms",
            normalizedQuery, items.Count, swTotal.Elapsed.TotalMilliseconds);

        return items;
    }

    public async Task<SearchSummaryResponse> SearchSummaryAsync(
        SearchSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        // Execute sequentially to prevent DbContext concurrency violations on the same scoped instance
        var postsResult = await SearchPostsAsync(new SearchPostsQuery(query.Query, null, query.PostLimit, query.CurrentUserId), cancellationToken);
        var usersResult = await SearchUsersAsync(new SearchUsersQuery(query.Query, query.UserLimit, query.CurrentUserId), cancellationToken);

        return new SearchSummaryResponse(
            postsResult.Items,
            usersResult
        );
    }

    private static Query BuildPostSearchQuery(string query, IReadOnlyList<string> blockedUserIds)
    {
        var shouldQueries = new List<Query>();

        if (query.Length <= 2)
        {
            // Short query: term / prefix, no fuzzy noise
            shouldQueries.Add(new MatchPhraseQuery("content")
            {
                Query = query,
                Boost = 3.0f
            });

            shouldQueries.Add(new PrefixQuery("content.ngram")
            {
                Value = query.ToLowerInvariant(),
                Boost = 2.0f
            });

            shouldQueries.Add(new PrefixQuery("author_username")
            {
                Value = query.ToLowerInvariant(),
                Boost = 2.0f
            });

            shouldQueries.Add(new TermQuery("tags")
            {
                Value = query.ToLowerInvariant().TrimStart('#'),
                Boost = 3.0f
            });
        }
        else
        {
            // Normal query (>= 3 chars): Full multi_match with Indonesian analysis & AUTO fuzziness
            shouldQueries.Add(new MultiMatchQuery
            {
                Query = query,
                Fields = new[] { "content^3", "tags^4", "author_username^2", "author_display_name" },
                Type = TextQueryType.BestFields,
                Fuzziness = new Fuzziness("AUTO"),
                Boost = 2.0f
            });

            // Exact phrase match receives high priority
            shouldQueries.Add(new MatchPhraseQuery("content")
            {
                Query = query,
                Boost = 5.0f
            });

            // Prefix/Autocomplete match on content
            shouldQueries.Add(new MatchBoolPrefixQuery("content.ngram")
            {
                Query = query,
                Boost = 1.5f
            });

            // Tag match
            shouldQueries.Add(new TermQuery("tags")
            {
                Value = query.ToLowerInvariant().TrimStart('#'),
                Boost = 4.0f
            });
        }

        var filterQueries = new List<Query>
        {
            new TermQuery("status") { Value = "ACTIVE" },
            new TermQuery("visibility") { Value = "PUBLIC" }
        };

        var mustNotQueries = new List<Query>();
        if (blockedUserIds.Count > 0)
        {
            var termsList = blockedUserIds.Select(FieldValue.String).ToList();
            mustNotQueries.Add(new TermsQuery
            {
                Field = "author_id",
                Terms = new TermsQueryField(termsList)
            });
        }

        return new BoolQuery
        {
            Should = shouldQueries,
            MinimumShouldMatch = 1,
            Filter = filterQueries,
            MustNot = mustNotQueries.Count > 0 ? mustNotQueries : null
        };
    }

    private static Query BuildUserSearchQuery(string query, IReadOnlyList<string> blockedUserIds)
    {
        var lowerQuery = query.ToLowerInvariant();
        var shouldQueries = new List<Query>
        {
            // 1. Exact username match (Top Priority)
            new TermQuery("username")
            {
                Value = lowerQuery,
                Boost = 10.0f
            },

            // 2. Username prefix match
            new PrefixQuery("username")
            {
                Value = lowerQuery,
                Boost = 7.0f
            },

            // 3. Exact display name match
            new TermQuery("display_name.keyword")
            {
                Value = query,
                Boost = 5.0f
            },

            // 4. Display name prefix
            new MatchPhrasePrefixQuery("display_name")
            {
                Query = query,
                Boost = 4.0f
            }
        };

        // Fuzzy matches only for queries >= 3 chars to avoid noise
        if (query.Length >= 3)
        {
            // 5. Fuzzy username
            shouldQueries.Add(new FuzzyQuery("username")
            {
                Value = lowerQuery,
                Fuzziness = new Fuzziness("AUTO"),
                Boost = 3.0f
            });

            // 6. Fuzzy display name
            shouldQueries.Add(new MatchQuery("display_name")
            {
                Query = query,
                Fuzziness = new Fuzziness("AUTO"),
                Boost = 2.0f
            });

            // 7. Bio match
            shouldQueries.Add(new MatchQuery("bio")
            {
                Query = query,
                Boost = 1.0f
            });
        }

        var filterQueries = new List<Query>
        {
            new TermQuery("status") { Value = "ACTIVE" }
        };

        var mustNotQueries = new List<Query>();
        if (blockedUserIds.Count > 0)
        {
            var termsList = blockedUserIds.Select(FieldValue.String).ToList();
            mustNotQueries.Add(new TermsQuery
            {
                Field = "id",
                Terms = new TermsQueryField(termsList)
            });
        }

        return new BoolQuery
        {
            Should = shouldQueries,
            MinimumShouldMatch = 1,
            Filter = filterQueries,
            MustNot = mustNotQueries.Count > 0 ? mustNotQueries : null
        };
    }

    private static string NormalizeQuery(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var trimmed = input.Trim();
        if (trimmed.Length > MaxQueryLength)
        {
            trimmed = trimmed[..MaxQueryLength];
        }
        return Regex.Replace(trimmed, @"\s+", " ");
    }

    private static string? HighlightText(string? text, string term)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(term))
            return text;

        try
        {
            var pattern = Regex.Escape(term);
            return Regex.Replace(text, pattern, "<mark>$&</mark>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return text;
        }
    }

    private async Task<IReadOnlyList<SearchUserItemDto>> FallbackSearchUsersFromDbAsync(
        string normalizedQuery,
        int limit,
        HashSet<Guid> blockedUserIds,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var lowerQuery = normalizedQuery.ToLowerInvariant();

        var query = _dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsSuspended);

        if (blockedUserIds.Count > 0)
        {
            query = query.Where(u => !blockedUserIds.Contains(u.Id));
        }

        query = query.Where(u =>
            u.Username.ToLower().Contains(lowerQuery) ||
            u.DisplayName.ToLower().Contains(lowerQuery) ||
            (u.Bio != null && u.Bio.ToLower().Contains(lowerQuery)));

        var matchedUsers = await query
            .Take(limit * 2)
            .ToListAsync(cancellationToken);

        var sortedUsers = matchedUsers
            .OrderByDescending(u => u.Username.Equals(lowerQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(u => u.Username.StartsWith(lowerQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(u => u.DisplayName.Equals(lowerQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(u => u.DisplayName.StartsWith(lowerQuery, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();

        HashSet<Guid> followingUserIds = new();
        if (currentUserId.HasValue && sortedUsers.Count > 0)
        {
            var userIds = sortedUsers.Select(u => u.Id).ToList();
            followingUserIds = (await _dbContext.Follows
                .AsNoTracking()
                .Where(f => f.FollowerId == currentUserId.Value && userIds.Contains(f.FollowingId))
                .Select(f => f.FollowingId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var results = sortedUsers.Select(u => new SearchUserItemDto(
            u.Id,
            u.Username,
            u.DisplayName,
            u.Bio,
            u.AvatarUrl,
            u.IsVerified,
            HighlightText(u.Username, normalizedQuery),
            HighlightText(u.DisplayName, normalizedQuery),
            u.Bio != null ? HighlightText(u.Bio, normalizedQuery) : null,
            followingUserIds.Contains(u.Id),
            1.0
        )).ToList();

        sw.Stop();
        SearchMetrics.SearchLatency.Record(sw.Elapsed.TotalMilliseconds);
        SearchMetrics.SearchResultsCount.Record(results.Count);

        _logger.LogInformation("FallbackSearchUsersFromDb: query='{Query}' returned {Count} users in {Duration}ms",
            normalizedQuery, results.Count, sw.Elapsed.TotalMilliseconds);

        return results;
    }

    private async Task<CursorPagedResult<SearchPostItemDto>> FallbackSearchPostsFromDbAsync(
        string normalizedQuery,
        string? rawCursor,
        int pageSize,
        HashSet<Guid> blockedUserIds,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var lowerQuery = normalizedQuery.ToLowerInvariant();

        var query = _dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.MediaAttachments)
                .ThenInclude(pm => pm.Media)
            .Where(p => !p.IsDeleted && !p.IsHidden);

        if (blockedUserIds.Count > 0)
        {
            query = query.Where(p => !blockedUserIds.Contains(p.AuthorId));
        }

        query = query.Where(p =>
            p.Content.ToLower().Contains(lowerQuery) ||
            (p.Author != null && p.Author.Username.ToLower().Contains(lowerQuery)) ||
            (p.Author != null && p.Author.DisplayName.ToLower().Contains(lowerQuery)));

        if (!string.IsNullOrWhiteSpace(rawCursor) && SearchCursor.TryDecode(rawCursor, out var decodedCursor, out _) && decodedCursor != null)
        {
            var cursorTime = DateTimeOffset.FromUnixTimeMilliseconds(decodedCursor.CreatedAtMs);
            if (Guid.TryParse(decodedCursor.Id, out var cursorGuid))
            {
                query = query.Where(p => p.CreatedAt < cursorTime || (p.CreatedAt == cursorTime && p.Id.CompareTo(cursorGuid) < 0));
            }
            else
            {
                query = query.Where(p => p.CreatedAt < cursorTime);
            }
        }

        query = query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);

        var posts = await query.Take(pageSize + 1).ToListAsync(cancellationToken);

        bool hasNextPage = posts.Count > pageSize;
        var resultPosts = hasNextPage ? posts.Take(pageSize).ToList() : posts;

        var postIds = resultPosts.Select(p => p.Id).ToList();
        HashSet<Guid> likedPostIds = new();
        HashSet<Guid> bookmarkedPostIds = new();

        if (currentUserId.HasValue && postIds.Count > 0)
        {
            likedPostIds = (await _dbContext.PostLikes
                .AsNoTracking()
                .Where(pl => pl.UserId == currentUserId.Value && postIds.Contains(pl.PostId))
                .Select(pl => pl.PostId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            bookmarkedPostIds = (await _dbContext.PostBookmarks
                .AsNoTracking()
                .Where(pb => pb.UserId == currentUserId.Value && postIds.Contains(pb.PostId))
                .Select(pb => pb.PostId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var items = resultPosts.Select(p =>
        {
            var authorDto = new PostAuthorDto(
                p.Author != null ? p.Author.Id : p.AuthorId,
                p.Author?.Username ?? "unknown",
                p.Author?.DisplayName ?? "Unknown",
                p.Author?.AvatarUrl,
                p.Author?.IsVerified ?? false
            );

            var mediaDtos = p.MediaAttachments
                .OrderBy(pm => pm.Position)
                .Where(pm => pm.Media != null)
                .Select(pm => new PostMediaDto(
                    pm.MediaId,
                    $"/api/v1/media/{pm.MediaId}",
                    pm.Media!.OriginalFileName,
                    pm.Media.MimeType,
                    pm.Position
                ))
                .ToList();

            return new SearchPostItemDto(
                Id: p.Id,
                Content: p.Content,
                HighlightedContent: HighlightText(p.Content, normalizedQuery),
                Author: authorDto,
                Media: mediaDtos,
                ReplyCount: p.ReplyCount,
                MediaCount: p.MediaCount,
                LikeCount: p.LikeCount,
                BookmarkCount: p.BookmarkCount,
                Liked: likedPostIds.Contains(p.Id),
                Bookmarked: bookmarkedPostIds.Contains(p.Id),
                IsDeleted: false,
                CreatedAt: p.CreatedAt,
                UpdatedAt: p.UpdatedAt,
                Score: 1.0
            );
        }).ToList();

        string? nextCursor = null;
        if (hasNextPage && resultPosts.Count > 0)
        {
            var last = resultPosts[^1];
            nextCursor = new SearchCursor(1.0, last.CreatedAt.ToUnixTimeMilliseconds(), last.Id.ToString()).Encode();
        }

        sw.Stop();
        SearchMetrics.SearchLatency.Record(sw.Elapsed.TotalMilliseconds);
        SearchMetrics.SearchResultsCount.Record(items.Count);

        _logger.LogInformation("FallbackSearchPostsFromDb: query='{Query}' returned {Count} posts (hasNext: {HasNext}) in {Duration}ms",
            normalizedQuery, items.Count, hasNextPage, sw.Elapsed.TotalMilliseconds);

        return new CursorPagedResult<SearchPostItemDto>
        {
            Items = items,
            NextCursor = nextCursor,
            HasNextPage = hasNextPage,
            PageSize = pageSize
        };
    }
}
