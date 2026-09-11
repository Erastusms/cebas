using System.Diagnostics.Metrics;

namespace CEBAS.Infrastructure.Observability;

public static class SearchMetrics
{
    public const string MeterName = "CEBAS.Search";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> SearchRequestCount = Meter.CreateCounter<long>(
        "search_requests_total",
        description: "Total number of search requests executed");

    public static readonly Histogram<double> SearchLatency = Meter.CreateHistogram<double>(
        "search_request_duration_ms",
        unit: "ms",
        description: "Latency of search requests in milliseconds");

    public static readonly Counter<long> SearchErrorsCount = Meter.CreateCounter<long>(
        "search_errors_total",
        description: "Number of search requests that resulted in an error");

    public static readonly Histogram<long> SearchResultsCount = Meter.CreateHistogram<long>(
        "search_results_count",
        description: "Number of search items returned per query");

    public static readonly Histogram<double> ElasticsearchLatency = Meter.CreateHistogram<double>(
        "elasticsearch_request_duration_ms",
        unit: "ms",
        description: "Latency of raw Elasticsearch queries in milliseconds");

    public static readonly Counter<long> IndexingThroughput = Meter.CreateCounter<long>(
        "search_indexed_documents_total",
        description: "Total number of documents indexed into Elasticsearch via outbox projections");

    public static readonly Counter<long> BulkIndexingThroughput = Meter.CreateCounter<long>(
        "search_bulk_reindexed_documents_total",
        description: "Total number of documents indexed via bulk reindexer");

    public static readonly Counter<long> ProjectionFailuresCount = Meter.CreateCounter<long>(
        "search_projection_failures_total",
        description: "Total number of outbox-to-Elasticsearch projection failures");

    public static readonly Histogram<double> ProjectionLatency = Meter.CreateHistogram<double>(
        "search_projection_duration_ms",
        unit: "ms",
        description: "Duration of processing an individual search projection in milliseconds");
}
