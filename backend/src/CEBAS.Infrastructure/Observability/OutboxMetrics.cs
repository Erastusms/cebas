using System.Diagnostics.Metrics;

namespace CEBAS.Infrastructure.Observability;

public static class OutboxMetrics
{
    public const string MeterName = "CEBAS.Outbox";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> EventsPolledCount = Meter.CreateCounter<long>(
        "outbox_events_polled_count",
        description: "Number of outbox events claimed by workers");

    public static readonly Counter<long> EventsPublishedCount = Meter.CreateCounter<long>(
        "outbox_events_published_count",
        description: "Number of outbox events successfully published to Redis Pub/Sub");

    public static readonly Counter<long> EventsFailedCount = Meter.CreateCounter<long>(
        "outbox_events_failed_count",
        description: "Number of outbox events that permanently failed after exceeding retry limits");

    public static readonly Counter<long> EventsRetriedCount = Meter.CreateCounter<long>(
        "outbox_events_retried_count",
        description: "Number of outbox event publish attempts that encountered transient errors and were rescheduled");

    public static readonly Histogram<double> PublishLatency = Meter.CreateHistogram<double>(
        "outbox_publish_latency_ms",
        unit: "ms",
        description: "Latency duration of publishing an event to Redis in milliseconds");

    public static readonly Histogram<double> BatchProcessingDuration = Meter.CreateHistogram<double>(
        "outbox_batch_processing_duration_ms",
        unit: "ms",
        description: "Duration of processing an entire outbox batch in milliseconds");
}
