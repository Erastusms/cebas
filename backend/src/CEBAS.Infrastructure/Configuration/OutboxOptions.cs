namespace CEBAS.Infrastructure.Configuration;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollIntervalMs { get; set; } = 50;
    public int BatchSize { get; set; } = 50;
    public int MaxRetryAttempts { get; set; } = 5;
    public int ProcessingLockTimeoutSeconds { get; set; } = 60;
}
