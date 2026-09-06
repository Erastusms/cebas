using System.Diagnostics.Metrics;

namespace CEBAS.Infrastructure.Observability;

/// <summary>
/// Centralized production metrics for HTTP, CQRS, Database, Realtime, Safety, and System events.
/// All metrics enforce strictly bounded cardinality on tags/labels.
/// </summary>
public static class CebasMetrics
{
    public const string MeterName = "CEBAS.Core";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    // HTTP / API metrics
    public static readonly Counter<long> HttpRequestCount = Meter.CreateCounter<long>(
        "http_requests_total",
        description: "Total number of HTTP requests processed");

    public static readonly Counter<long> HttpErrorCount = Meter.CreateCounter<long>(
        "http_errors_total",
        description: "Total number of HTTP requests that resulted in 4xx or 5xx responses");

    public static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>(
        "http_request_duration_ms",
        unit: "ms",
        description: "HTTP request execution duration in milliseconds");

    // CQRS Metrics
    public static readonly Counter<long> CommandExecutionCount = Meter.CreateCounter<long>(
        "cqrs_commands_total",
        description: "Total number of CQRS commands processed");

    public static readonly Counter<long> CommandFailureCount = Meter.CreateCounter<long>(
        "cqrs_command_failures_total",
        description: "Total number of CQRS commands that threw exceptions or failed validation");

    public static readonly Histogram<double> CommandDuration = Meter.CreateHistogram<double>(
        "cqrs_command_duration_ms",
        unit: "ms",
        description: "Duration of CQRS command execution in milliseconds");

    public static readonly Counter<long> QueryExecutionCount = Meter.CreateCounter<long>(
        "cqrs_queries_total",
        description: "Total number of CQRS queries executed");

    public static readonly Counter<long> QueryFailureCount = Meter.CreateCounter<long>(
        "cqrs_query_failures_total",
        description: "Total number of CQRS queries that failed");

    public static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>(
        "cqrs_query_duration_ms",
        unit: "ms",
        description: "Duration of CQRS query execution in milliseconds");

    // Database Metrics
    public static readonly Histogram<double> DatabaseQueryDuration = Meter.CreateHistogram<double>(
        "database_query_duration_ms",
        unit: "ms",
        description: "Execution duration of critical database operations in milliseconds");

    public static readonly Counter<long> DatabaseQueryFailureCount = Meter.CreateCounter<long>(
        "database_query_failures_total",
        description: "Total number of failed database queries");

    // Realtime SignalR Metrics
    public static readonly UpDownCounter<long> ActiveRealtimeConnections = Meter.CreateUpDownCounter<long>(
        "realtime_active_connections",
        description: "Number of currently active SignalR connections");

    public static readonly Counter<long> RealtimeConnectionFailures = Meter.CreateCounter<long>(
        "realtime_connection_failures_total",
        description: "Total number of realtime connection negotiation or transport failures");

    // Notification Metrics
    public static readonly Counter<long> NotificationsGenerated = Meter.CreateCounter<long>(
        "notifications_generated_total",
        description: "Total number of notifications created");

    public static readonly Counter<long> NotificationDeliveryAttempts = Meter.CreateCounter<long>(
        "notification_delivery_attempts_total",
        description: "Total number of notification delivery attempts");

    public static readonly Counter<long> NotificationDeliveryFailures = Meter.CreateCounter<long>(
        "notification_delivery_failures_total",
        description: "Total number of notification delivery failures");

    // Safety & Moderation Metrics
    public static readonly Counter<long> ReportsCreated = Meter.CreateCounter<long>(
        "moderation_reports_created_total",
        description: "Total number of user safety reports filed");

    public static readonly Counter<long> ModerationActions = Meter.CreateCounter<long>(
        "moderation_actions_total",
        description: "Total number of moderation actions applied (e.g. hide, suspend, dismiss)");

    public static readonly Counter<long> ModerationFailures = Meter.CreateCounter<long>(
        "moderation_processing_failures_total",
        description: "Total number of failed moderation actions");
}
