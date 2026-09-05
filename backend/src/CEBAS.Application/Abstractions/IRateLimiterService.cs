namespace CEBAS.Application.Abstractions;

public sealed record RateLimitResult(bool IsAllowed, int RemainingPermits, int RetryAfterSeconds);

public interface IRateLimiterService
{
    Task<RateLimitResult> CheckRateLimitAsync(string key, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default);
}
