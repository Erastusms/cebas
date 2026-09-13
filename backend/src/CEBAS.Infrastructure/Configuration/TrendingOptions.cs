namespace CEBAS.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for real-time trending calculation, sliding window decay, and retention.
/// </summary>
public class TrendingOptions
{
    public const string SectionName = "Trending";

    /// <summary>
    /// Rolling window duration in hours for trending aggregation (default 24).
    /// </summary>
    public int WindowHours { get; set; } = 24;

    /// <summary>
    /// Exponential decay coefficient lambda: weight = e^(-lambda * ageInHours).
    /// Default 0.05 gives recent hours higher weight while smoothly decaying past 24 hours.
    /// </summary>
    public double DecayLambda { get; set; } = 0.05;

    /// <summary>
    /// Default number of top trending topics to return (default 10).
    /// </summary>
    public int ResultLimit { get; set; } = 10;

    /// <summary>
    /// Retention period in hours for hourly Redis buckets (default 48 hours to provide a safe operational buffer).
    /// </summary>
    public int RetentionHours { get; set; } = 48;

    /// <summary>
    /// Interval in seconds for the background worker to pre-aggregate and refresh top trending topics (default 60s).
    /// </summary>
    public int AggregationIntervalSeconds { get; set; } = 60;
}
