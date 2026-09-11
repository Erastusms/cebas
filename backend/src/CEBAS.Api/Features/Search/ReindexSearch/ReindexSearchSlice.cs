using MediatR;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions.Search;
using CEBAS.Application.Contracts.Search;

namespace CEBAS.Api.Features.Search.ReindexSearch;

public sealed record ReindexSearchCommand(
    string Type = "all",
    int BatchSize = 100
) : IRequest<ReindexSummary>;

public sealed class ReindexSearchCommandHandler : IRequestHandler<ReindexSearchCommand, ReindexSummary>
{
    private readonly ISearchReindexer _reindexer;
    private readonly ISearchIndexManager _indexManager;
    private readonly ILogger<ReindexSearchCommandHandler> _logger;

    public ReindexSearchCommandHandler(
        ISearchReindexer reindexer,
        ISearchIndexManager indexManager,
        ILogger<ReindexSearchCommandHandler> logger)
    {
        _reindexer = reindexer;
        _indexManager = indexManager;
        _logger = logger;
    }

    public async Task<ReindexSummary> Handle(ReindexSearchCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin requested search reindex. Type: {Type}, BatchSize: {BatchSize}",
            request.Type, request.BatchSize);

        // Ensure indices and aliases exist
        await _indexManager.EnsureIndicesExistAsync(cancellationToken);

        int batchSize = Math.Clamp(request.BatchSize, 10, 500);

        return request.Type.ToLowerInvariant() switch
        {
            "posts" => await _reindexer.ReindexPostsAsync(batchSize, cancellationToken),
            "users" => await _reindexer.ReindexUsersAsync(batchSize, cancellationToken),
            _ => await _reindexer.ReindexAllAsync(batchSize, cancellationToken)
        };
    }
}
