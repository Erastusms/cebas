using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Contracts.Search;
using CEBAS.Infrastructure.Configuration;
using CEBAS.Infrastructure.Search.Models;

namespace CEBAS.Infrastructure.Search;

public class ElasticsearchIndexManager : ISearchIndexManager
{
    private readonly ElasticsearchClient _client;
    private readonly IOptions<ElasticsearchOptions> _options;
    private readonly ILogger<ElasticsearchIndexManager> _logger;

    public const string PostsVersionedIndex = "posts_index_v1";
    public const string UsersVersionedIndex = "users_index_v1";

    public ElasticsearchIndexManager(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchIndexManager> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task EnsureIndicesExistAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ping = await _client.PingAsync(cancellationToken);
            if (!ping.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch is not reachable at {Url}. Skipping index validation on startup.", _options.Value.Url);
                return;
            }

            await EnsurePostsIndexExistsAsync(cancellationToken);
            await EnsureUsersIndexExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure Elasticsearch indices exist on startup. Application will degrade search gracefully.");
        }
    }

    public async Task RecreateIndicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recreating Elasticsearch indices: {PostsIndex}, {UsersIndex}", PostsVersionedIndex, UsersVersionedIndex);

        var postsExists = await _client.Indices.ExistsAsync(PostsVersionedIndex, cancellationToken);
        if (postsExists.Exists)
        {
            await _client.Indices.DeleteAsync(PostsVersionedIndex, cancellationToken);
        }

        var usersExists = await _client.Indices.ExistsAsync(UsersVersionedIndex, cancellationToken);
        if (usersExists.Exists)
        {
            await _client.Indices.DeleteAsync(UsersVersionedIndex, cancellationToken);
        }

        await CreatePostsIndexAsync(cancellationToken);
        await CreateUsersIndexAsync(cancellationToken);
    }

    public async Task<SearchHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ping = await _client.PingAsync(cancellationToken);
            if (!ping.IsValidResponse)
            {
                return new SearchHealthStatus("Unavailable", false, "unknown", 0, 0);
            }

            var clusterHealth = await _client.Cluster.HealthAsync(cancellationToken);
            var status = clusterHealth.IsValidResponse ? clusterHealth.Status.ToString() : "Degraded";
            var clusterName = clusterHealth.IsValidResponse ? clusterHealth.ClusterName : "cebas-search-cluster";

            long postCount = 0;
            var postCountResp = await _client.CountAsync<PostDocument>(c => c.Indices(_options.Value.PostsIndex), cancellationToken);
            if (postCountResp.IsValidResponse)
            {
                postCount = postCountResp.Count;
            }

            long userCount = 0;
            var userCountResp = await _client.CountAsync<UserDocument>(c => c.Indices(_options.Value.UsersIndex), cancellationToken);
            if (userCountResp.IsValidResponse)
            {
                userCount = userCountResp.Count;
            }

            return new SearchHealthStatus(status, true, clusterName, postCount, userCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query Elasticsearch health");
            return new SearchHealthStatus("Unavailable", false, "error", 0, 0);
        }
    }

    private async Task EnsurePostsIndexExistsAsync(CancellationToken cancellationToken)
    {
        var exists = await _client.Indices.ExistsAsync(PostsVersionedIndex, cancellationToken);
        if (!exists.Exists)
        {
            await CreatePostsIndexAsync(cancellationToken);
        }
        else
        {
            // Ensure alias exists
            var aliasExists = await _client.Indices.ExistsAliasAsync(_options.Value.PostsIndex, cancellationToken);
            if (!aliasExists.Exists)
            {
                await _client.Indices.PutAliasAsync(PostsVersionedIndex, _options.Value.PostsIndex, cancellationToken);
            }
        }
    }

    private async Task EnsureUsersIndexExistsAsync(CancellationToken cancellationToken)
    {
        var exists = await _client.Indices.ExistsAsync(UsersVersionedIndex, cancellationToken);
        if (!exists.Exists)
        {
            await CreateUsersIndexAsync(cancellationToken);
        }
        else
        {
            // Ensure alias exists
            var aliasExists = await _client.Indices.ExistsAliasAsync(_options.Value.UsersIndex, cancellationToken);
            if (!aliasExists.Exists)
            {
                await _client.Indices.PutAliasAsync(UsersVersionedIndex, _options.Value.UsersIndex, cancellationToken);
            }
        }
    }

    private async Task CreatePostsIndexAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating Elasticsearch index {Index} with alias {Alias} and Indonesian analysis",
            PostsVersionedIndex, _options.Value.PostsIndex);

        var createResponse = await _client.Indices.CreateAsync<PostDocument>(PostsVersionedIndex, c => c
            .Settings(s => s
                .Analysis(a => a
                    .TokenFilters(tf => tf
                        .Stop("indonesian_stop", st => st.Stopwords(new[] { "_indonesian_" }))
                        .Stemmer("indonesian_stemmer", sm => sm.Language("indonesian"))
                        .EdgeNGram("edge_ngram_filter", ng => ng.MinGram(2).MaxGram(15))
                    )
                    .Analyzers(an => an
                        .Custom("indonesian_text", ca => ca
                            .Tokenizer("standard")
                            .Filter(new[] { "lowercase", "indonesian_stop", "indonesian_stemmer" })
                        )
                        .Custom("ngram_autocomplete", ca => ca
                            .Tokenizer("standard")
                            .Filter(new[] { "lowercase", "edge_ngram_filter" })
                        )
                    )
                )
            )
            .Mappings(m => m
                .Properties(p => p
                    .Keyword(k => k.Id)
                    .Text(t => t.Content, ct => ct
                        .Analyzer("indonesian_text")
                        .Fields(f => f
                            .Text("ngram", ng => ng.Analyzer("ngram_autocomplete").SearchAnalyzer("standard"))
                            .Keyword("keyword", kw => kw.IgnoreAbove(256))
                        )
                    )
                    .Keyword(k => k.AuthorId)
                    .Keyword(k => k.AuthorUsername, au => au
                        .Fields(f => f
                            .Text("prefix", pf => pf.Analyzer("ngram_autocomplete").SearchAnalyzer("standard"))
                        )
                    )
                    .Text(t => t.AuthorDisplayName, ad => ad
                        .Analyzer("indonesian_text")
                        .Fields(f => f
                            .Text("prefix", pf => pf.Analyzer("ngram_autocomplete").SearchAnalyzer("standard"))
                            .Keyword("keyword", kw => kw.IgnoreAbove(256))
                        )
                    )
                    .Keyword(k => k.Tags)
                    .Date(d => d.CreatedAt)
                    .Date(d => d.UpdatedAt)
                    .Keyword(k => k.ParentId)
                    .Keyword(k => k.Status)
                    .Keyword(k => k.Visibility)
                )
            )
        , cancellationToken);

        if (!createResponse.IsValidResponse)
        {
            _logger.LogError("Failed to create index {Index}: {DebugInfo}", PostsVersionedIndex, createResponse.DebugInformation);
        }
        else
        {
            await _client.Indices.PutAliasAsync(PostsVersionedIndex, _options.Value.PostsIndex, cancellationToken);
        }
    }

    private async Task CreateUsersIndexAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating Elasticsearch index {Index} with alias {Alias} and Indonesian/Prefix analysis",
            UsersVersionedIndex, _options.Value.UsersIndex);

        var createResponse = await _client.Indices.CreateAsync<UserDocument>(UsersVersionedIndex, c => c
            .Settings(s => s
                .Analysis(a => a
                    .TokenFilters(tf => tf
                        .Stop("indonesian_stop", st => st.Stopwords(new[] { "_indonesian_" }))
                        .Stemmer("indonesian_stemmer", sm => sm.Language("indonesian"))
                        .EdgeNGram("edge_ngram_filter", ng => ng.MinGram(2).MaxGram(15))
                    )
                    .Analyzers(an => an
                        .Custom("indonesian_text", ca => ca
                            .Tokenizer("standard")
                            .Filter(new[] { "lowercase", "indonesian_stop", "indonesian_stemmer" })
                        )
                        .Custom("ngram_autocomplete", ca => ca
                            .Tokenizer("standard")
                            .Filter(new[] { "lowercase", "edge_ngram_filter" })
                        )
                    )
                )
            )
            .Mappings(m => m
                .Properties(p => p
                    .Keyword(k => k.Id)
                    .Keyword(k => k.Username, un => un
                        .Fields(f => f
                            .Text("prefix", pf => pf.Analyzer("ngram_autocomplete").SearchAnalyzer("standard"))
                        )
                    )
                    .Text(t => t.DisplayName, dn => dn
                        .Analyzer("indonesian_text")
                        .Fields(f => f
                            .Text("prefix", pf => pf.Analyzer("ngram_autocomplete").SearchAnalyzer("standard"))
                            .Keyword("keyword", kw => kw.IgnoreAbove(256))
                        )
                    )
                    .Text(t => t.Bio, b => b.Analyzer("indonesian_text"))
                    .Keyword(k => k.AvatarUrl)
                    .Boolean(b => b.IsVerified)
                    .Keyword(k => k.Status)
                    .Date(d => d.CreatedAt)
                    .Date(d => d.UpdatedAt)
                )
            )
        , cancellationToken);

        if (!createResponse.IsValidResponse)
        {
            _logger.LogError("Failed to create index {Index}: {DebugInfo}", UsersVersionedIndex, createResponse.DebugInformation);
        }
        else
        {
            await _client.Indices.PutAliasAsync(UsersVersionedIndex, _options.Value.UsersIndex, cancellationToken);
        }
    }
}

