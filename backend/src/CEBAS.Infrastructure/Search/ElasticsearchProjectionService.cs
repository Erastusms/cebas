using System.Diagnostics;
using System.Text.RegularExpressions;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Contracts.Events;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Search.Models;

namespace CEBAS.Infrastructure.Search;

public class ElasticsearchProjectionService : ISearchProjectionService
{
    private readonly ElasticsearchClient _client;
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthorProfileCache _authorCache;
    private readonly IOptions<ElasticsearchOptions> _options;
    private readonly ILogger<ElasticsearchProjectionService> _logger;

    private static readonly Regex HashtagRegex = new(@"#([a-zA-Z0-9_]+)", RegexOptions.Compiled);

    public ElasticsearchProjectionService(
        ElasticsearchClient client,
        ApplicationDbContext dbContext,
        IAuthorProfileCache authorCache,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchProjectionService> logger)
    {
        _client = client;
        _dbContext = dbContext;
        _authorCache = authorCache;
        _options = options;
        _logger = logger;
    }

    public async Task ProjectPostCreatedAsync(PostCreatedPayload payload, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var author = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == payload.AuthorId, cancellationToken);

            var tags = ExtractTags(payload.Content);

            var doc = new PostDocument
            {
                Id = payload.PostId.ToString(),
                Content = payload.Content ?? string.Empty,
                AuthorId = payload.AuthorId.ToString(),
                AuthorUsername = author?.Username ?? string.Empty,
                AuthorDisplayName = author?.DisplayName ?? author?.Username ?? string.Empty,
                Tags = tags,
                CreatedAt = payload.CreatedAt,
                UpdatedAt = payload.CreatedAt,
                ParentId = null,
                Status = "ACTIVE",
                Visibility = "PUBLIC"
            };

            var response = await _client.IndexAsync(doc, idx => idx
                .Index(_options.Value.PostsIndex)
                .Id(doc.Id), cancellationToken);

            if (!response.IsValidResponse)
            {
                SearchMetrics.ProjectionFailuresCount.Add(1);
                _logger.LogError("Failed to index post {PostId} in Elasticsearch: {DebugInfo}",
                    payload.PostId, response.DebugInformation);
                throw new InvalidOperationException($"Elasticsearch index failure for post {payload.PostId}");
            }

            SearchMetrics.IndexingThroughput.Add(1);
            _logger.LogInformation("Successfully projected POST_CREATED for post {PostId}", payload.PostId);
        }
        catch (Exception ex)
        {
            SearchMetrics.ProjectionFailuresCount.Add(1);
            _logger.LogError(ex, "Error projecting POST_CREATED for post {PostId}", payload.PostId);
            throw;
        }
        finally
        {
            sw.Stop();
            SearchMetrics.ProjectionLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task ProjectPostDeletedAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await _client.DeleteAsync<PostDocument>(postId.ToString(), d => d
                .Index(_options.Value.PostsIndex), cancellationToken);

            // 404 is considered successful idempotent deletion
            if (!response.IsValidResponse && response.ApiCallDetails?.HttpStatusCode != 404)
            {
                SearchMetrics.ProjectionFailuresCount.Add(1);
                _logger.LogWarning("Failed to delete post {PostId} from Elasticsearch: {DebugInfo}",
                    postId, response.DebugInformation);
            }
            else
            {
                _logger.LogInformation("Successfully projected POST_DELETED for post {PostId}", postId);
            }
        }
        catch (Exception ex)
        {
            SearchMetrics.ProjectionFailuresCount.Add(1);
            _logger.LogError(ex, "Error projecting POST_DELETED for post {PostId}", postId);
            throw;
        }
        finally
        {
            sw.Stop();
            SearchMetrics.ProjectionLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task ProjectPostUpdatedAsync(PostUpdatedPayload payload, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var tags = ExtractTags(payload.Content);

            var response = await _client.UpdateAsync<PostDocument, object>(_options.Value.PostsIndex, payload.PostId.ToString(), u => u
                .Doc(new
                {
                    content = payload.Content ?? string.Empty,
                    tags,
                    updated_at = payload.OccurredAt
                }), cancellationToken);

            if (!response.IsValidResponse && response.ApiCallDetails?.HttpStatusCode != 404)
            {
                SearchMetrics.ProjectionFailuresCount.Add(1);
                _logger.LogWarning("Failed to update post {PostId} in Elasticsearch: {DebugInfo}",
                    payload.PostId, response.DebugInformation);
            }
            else
            {
                _logger.LogInformation("Successfully projected POST_UPDATED for post {PostId}", payload.PostId);
            }
        }
        catch (Exception ex)
        {
            SearchMetrics.ProjectionFailuresCount.Add(1);
            _logger.LogError(ex, "Error projecting POST_UPDATED for post {PostId}", payload.PostId);
            throw;
        }
        finally
        {
            sw.Stop();
            SearchMetrics.ProjectionLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task ProjectProfileUpdatedAsync(ProfileUpdatedPayload payload, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var userDoc = new UserDocument
            {
                Id = payload.UserId.ToString(),
                Username = payload.Username,
                DisplayName = payload.DisplayName,
                Bio = payload.Bio,
                AvatarUrl = payload.AvatarUrl,
                IsVerified = false,
                Status = "ACTIVE",
                UpdatedAt = payload.OccurredAt
            };

            var userResponse = await _client.IndexAsync(userDoc, idx => idx
                .Index(_options.Value.UsersIndex)
                .Id(userDoc.Id), cancellationToken);

            if (!userResponse.IsValidResponse)
            {
                SearchMetrics.ProjectionFailuresCount.Add(1);
                _logger.LogError("Failed to update user {UserId} in Elasticsearch: {DebugInfo}",
                    payload.UserId, userResponse.DebugInformation);
            }

            // Also update denormalized author display names on author's posts
            try
            {
                await _client.UpdateByQueryAsync<PostDocument>(_options.Value.PostsIndex, u => u
                    .Query(q => q.Term(t => t.Field(f => f.AuthorId).Value(payload.UserId.ToString())))
                    .Script(s => s
                        .Source("ctx._source.author_username = params.username; ctx._source.author_display_name = params.displayName")
                        .Params(p => p
                            .Add("username", payload.Username)
                            .Add("displayName", payload.DisplayName)
                        )
                    ), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Non-critical: Failed to update denormalized posts for user {UserId}", payload.UserId);
            }

            _logger.LogInformation("Successfully projected PROFILE_UPDATED for user {UserId}", payload.UserId);
        }
        catch (Exception ex)
        {
            SearchMetrics.ProjectionFailuresCount.Add(1);
            _logger.LogError(ex, "Error projecting PROFILE_UPDATED for user {UserId}", payload.UserId);
            throw;
        }
        finally
        {
            sw.Stop();
            SearchMetrics.ProjectionLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task ProjectUserUpdatedAsync(UserUpdatedPayload payload, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var userDoc = new UserDocument
            {
                Id = payload.UserId.ToString(),
                Username = payload.Username,
                DisplayName = payload.DisplayName,
                Bio = payload.Bio,
                AvatarUrl = payload.AvatarUrl,
                Status = payload.Status,
                UpdatedAt = payload.OccurredAt
            };

            await _client.IndexAsync(userDoc, idx => idx
                .Index(_options.Value.UsersIndex)
                .Id(userDoc.Id), cancellationToken);

            _logger.LogInformation("Successfully projected USER_UPDATED for user {UserId}", payload.UserId);
        }
        catch (Exception ex)
        {
            SearchMetrics.ProjectionFailuresCount.Add(1);
            _logger.LogError(ex, "Error projecting USER_UPDATED for user {UserId}", payload.UserId);
            throw;
        }
        finally
        {
            sw.Stop();
            SearchMetrics.ProjectionLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task ProjectUserCreatedAsync(UserCreatedPayload payload, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var userDoc = new UserDocument
            {
                Id = payload.UserId.ToString(),
                Username = payload.Username,
                DisplayName = payload.DisplayName,
                Bio = payload.Bio,
                AvatarUrl = payload.AvatarUrl,
                IsVerified = false,
                Status = "ACTIVE",
                CreatedAt = payload.CreatedAt,
                UpdatedAt = payload.CreatedAt
            };

            await _client.IndexAsync(userDoc, idx => idx
                .Index(_options.Value.UsersIndex)
                .Id(userDoc.Id), cancellationToken);

            _logger.LogInformation("Successfully projected USER_CREATED for user {UserId}", payload.UserId);
        }
        catch (Exception ex)
        {
            SearchMetrics.ProjectionFailuresCount.Add(1);
            _logger.LogError(ex, "Error projecting USER_CREATED for user {UserId}", payload.UserId);
            throw;
        }
        finally
        {
            sw.Stop();
            SearchMetrics.ProjectionLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task ProjectPostHiddenAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        await ProjectPostDeletedAsync(postId, cancellationToken);
    }

    public async Task ProjectUserSuspendedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _client.UpdateAsync<UserDocument, object>(_options.Value.UsersIndex, userId.ToString(), u => u
                .Doc(new { status = "SUSPENDED" }), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting UserSuspended for user {UserId}", userId);
        }
        finally
        {
            sw.Stop();
            SearchMetrics.ProjectionLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task ProjectUserReinstatedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _client.UpdateAsync<UserDocument, object>(_options.Value.UsersIndex, userId.ToString(), u => u
                .Doc(new { status = "ACTIVE" }), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting UserReinstated for user {UserId}", userId);
        }
        finally
        {
            sw.Stop();
            SearchMetrics.ProjectionLatency.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    private static List<string> ExtractTags(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return new List<string>();

        var matches = HashtagRegex.Matches(content);
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                tags.Add(match.Groups[1].Value.ToLowerInvariant());
            }
        }
        return tags.ToList();
    }
}
