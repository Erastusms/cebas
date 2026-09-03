using System.Diagnostics.Metrics;

namespace CEBAS.Infrastructure.Observability;

/// <summary>
/// Centralized observability metrics for timeline engine operations (Phase 6 / ADR-03).
/// Records throughput, latencies, database query duration, and client cursor errors.
/// </summary>
public static class TimelineMetrics
{
    public const string MeterName = "CEBAS.Timelines";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> HomeRequestCount = Meter.CreateCounter<long>(
        "timeline_home_request_count",
        description: "Number of timeline home feed requests received");

    public static readonly Counter<long> HomeErrorCount = Meter.CreateCounter<long>(
        "timeline_home_error_count",
        description: "Number of timeline home feed errors encountered");

    public static readonly Histogram<double> HomeLatency = Meter.CreateHistogram<double>(
        "timeline_home_latency",
        unit: "ms",
        description: "Latency duration of home feed requests in milliseconds");

    public static readonly Counter<long> ProfilePostsRequestCount = Meter.CreateCounter<long>(
        "timeline_profile_posts_request_count",
        description: "Number of timeline profile posts requests received");

    public static readonly Histogram<double> ProfilePostsLatency = Meter.CreateHistogram<double>(
        "timeline_profile_posts_latency",
        unit: "ms",
        description: "Latency duration of profile posts requests in milliseconds");

    public static readonly Counter<long> ProfileLikesRequestCount = Meter.CreateCounter<long>(
        "timeline_profile_likes_request_count",
        description: "Number of timeline profile likes requests received");

    public static readonly Histogram<double> ProfileLikesLatency = Meter.CreateHistogram<double>(
        "timeline_profile_likes_latency",
        unit: "ms",
        description: "Latency duration of profile likes requests in milliseconds");

    public static readonly Counter<long> CursorInvalidCount = Meter.CreateCounter<long>(
        "timeline_cursor_invalid_count",
        description: "Number of invalid, malformed, or out-of-bounds cursor tokens supplied by clients");

    public static readonly Histogram<double> DatabaseQueryDuration = Meter.CreateHistogram<double>(
        "timeline_database_query_duration",
        unit: "ms",
        description: "Execution time for timeline database queries in milliseconds");
}
