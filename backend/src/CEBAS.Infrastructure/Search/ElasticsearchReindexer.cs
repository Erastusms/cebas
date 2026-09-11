using System.Diagnostics;
using System.Text.RegularExpressions;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Contracts.Search;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Observability;
using CEBAS.Infrastructure.Persistence;
using CEBAS.Infrastructure.Search.Models;

namespace CEBAS.Infrastructure.Search;

public class ElasticsearchReindexer : ISearchReindexer
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ElasticsearchClient _client;
    private readonly IOptions<ElasticsearchOptions> _options;
    private readonly ILogger<ElasticsearchReindexer> _logger;

    private static readonly Regex HashtagRegex = new(@"#([a-zA-Z0-9_]+)", RegexOptions.Compiled);

    public ElasticsearchReindexer(
        ApplicationDbContext dbContext,
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchReindexer> logger)
    {
        _dbContext = dbContext;
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<ReindexSummary> ReindexAllAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Starting bulk reindex of all PostgreSQL data to Elasticsearch (batch size: {BatchSize})", batchSize);

        var postsSummary = await ReindexPostsAsync(batchSize, cancellationToken);
        var usersSummary = await ReindexUsersAsync(batchSize, cancellationToken);

        sw.Stop();
        _logger.LogInformation("Bulk reindexing completed in {Elapsed}ms. Processed {Posts} posts, {Users} users, {Errors} errors.",
            sw.Elapsed.TotalMilliseconds, postsSummary.ProcessedPosts, usersSummary.ProcessedUsers, postsSummary.Errors + usersSummary.Errors);

        return new ReindexSummary(
            postsSummary.ProcessedPosts,
            usersSummary.ProcessedUsers,
            postsSummary.Errors + usersSummary.Errors,
            sw.Elapsed.TotalMilliseconds
        );
    }

    public async Task<ReindexSummary> ReindexPostsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int processedCount = 0;
        int errorCount = 0;

        _logger.LogInformation("Starting bulk reindex of Posts...");

        DateTimeOffset lastCreatedAt = DateTimeOffset.MinValue;
        Guid lastId = Guid.Empty;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _dbContext.Posts
                .AsNoTracking()
                .Include(p => p.Author)
                .Where(p => !p.IsDeleted && !p.IsHidden)
                .Where(p => p.CreatedAt > lastCreatedAt || (p.CreatedAt == lastCreatedAt && p.Id > lastId))
                .OrderBy(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            var postDocuments = batch.Select(p => new PostDocument
            {
                Id = p.Id.ToString(),
                Content = p.Content,
                AuthorId = p.AuthorId.ToString(),
                AuthorUsername = p.Author?.Username ?? string.Empty,
                AuthorDisplayName = p.Author?.DisplayName ?? p.Author?.Username ?? string.Empty,
                Tags = ExtractTags(p.Content),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt ?? p.CreatedAt,
                ParentId = null,
                Status = "ACTIVE",
                Visibility = "PUBLIC"
            }).ToList();

            try
            {
                var bulkResponse = await _client.BulkAsync(b => b
                    .Index(_options.Value.PostsIndex)
                    .IndexMany(postDocuments, (descriptor, doc) => descriptor.Id(doc.Id)), cancellationToken);

                if (!bulkResponse.IsValidResponse)
                {
                    errorCount += batch.Count;
                    _logger.LogError("Error during bulk indexing posts batch: {DebugInfo}", bulkResponse.DebugInformation);
                }
                else
                {
                    processedCount += batch.Count;
                    SearchMetrics.BulkIndexingThroughput.Add(batch.Count);
                }
            }
            catch (Exception ex)
            {
                errorCount += batch.Count;
                _logger.LogError(ex, "Exception during bulk indexing posts batch");
            }

            var lastPost = batch[^1];
            lastCreatedAt = lastPost.CreatedAt;
            lastId = lastPost.Id;

            _logger.LogInformation("Reindexed {ProcessedCount} posts so far...", processedCount);
        }

        sw.Stop();
        return new ReindexSummary(processedCount, 0, errorCount, sw.Elapsed.TotalMilliseconds);
    }

    public async Task<ReindexSummary> ReindexUsersAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int processedCount = 0;
        int errorCount = 0;

        _logger.LogInformation("Starting bulk reindex of Users...");

        Guid lastId = Guid.Empty;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _dbContext.Users
                .AsNoTracking()
                .Where(u => !u.IsSuspended)
                .Where(u => u.Id > lastId)
                .OrderBy(u => u.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            var userDocuments = batch.Select(u => new UserDocument
            {
                Id = u.Id.ToString(),
                Username = u.Username,
                DisplayName = u.DisplayName,
                Bio = u.Bio,
                AvatarUrl = u.AvatarUrl,
                IsVerified = u.IsVerified,
                Status = "ACTIVE",
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt ?? u.CreatedAt
            }).ToList();

            try
            {
                var bulkResponse = await _client.BulkAsync(b => b
                    .Index(_options.Value.UsersIndex)
                    .IndexMany(userDocuments, (descriptor, doc) => descriptor.Id(doc.Id)), cancellationToken);

                if (!bulkResponse.IsValidResponse)
                {
                    errorCount += batch.Count;
                    _logger.LogError("Error during bulk indexing users batch: {DebugInfo}", bulkResponse.DebugInformation);
                }
                else
                {
                    processedCount += batch.Count;
                    SearchMetrics.BulkIndexingThroughput.Add(batch.Count);
                }
            }
            catch (Exception ex)
            {
                errorCount += batch.Count;
                _logger.LogError(ex, "Exception during bulk indexing users batch");
            }

            var lastUser = batch[^1];
            lastId = lastUser.Id;

            _logger.LogInformation("Reindexed {ProcessedCount} users so far...", processedCount);
        }

        sw.Stop();
        return new ReindexSummary(0, processedCount, errorCount, sw.Elapsed.TotalMilliseconds);
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
