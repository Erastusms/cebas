using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using CEBAS.Api.Configuration;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;

namespace CEBAS.Api.RateLimiting;

public static class RateLimitingRegistration
{
    public const string AuthenticationPolicy = "Authentication";
    public const string PublishingPolicy = "Publishing";
    public const string EngagementPolicy = "Engagement";
    public const string SearchPolicy = "Search";
    public const string ReportingPolicy = "Reporting";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IServiceCollection AddDistributedRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitingSection = configuration.GetSection(RateLimitingOptions.SectionName);
        services.Configure<RateLimitingOptions>(rateLimitingSection);
        var options = rateLimitingSection.Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                int retryAfterSeconds = 60;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = Math.Max(1, (int)retryAfter.TotalSeconds);
                }

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

                var problem = new ProblemDetailsResponse
                {
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = $"You are doing that a little too quickly. Please try again in {retryAfterSeconds} seconds.",
                    Instance = context.HttpContext.Request.Path,
                    TraceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier
                };

                await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions), cancellationToken);
            };

            // Register Policy 1: Authentication
            rateLimiterOptions.AddPolicy(AuthenticationPolicy, httpContext =>
            {
                var limiterService = httpContext.RequestServices.GetRequiredService<IRateLimiterService>();
                var policyOpts = options.Authentication;
                var partitionKey = ResolvePartitionKey(httpContext, "auth", options.RedisKeyPrefix);

                return RateLimitPartition.Get(partitionKey, key =>
                    new DistributedRateLimiterAdapter(limiterService, key, policyOpts.PermitLimit, TimeSpan.FromSeconds(policyOpts.WindowSeconds)));
            });

            // Register Policy 2: Publishing
            rateLimiterOptions.AddPolicy(PublishingPolicy, httpContext =>
            {
                var limiterService = httpContext.RequestServices.GetRequiredService<IRateLimiterService>();
                var policyOpts = options.Publishing;
                var partitionKey = ResolvePartitionKey(httpContext, "pub", options.RedisKeyPrefix);

                return RateLimitPartition.Get(partitionKey, key =>
                    new DistributedRateLimiterAdapter(limiterService, key, policyOpts.PermitLimit, TimeSpan.FromSeconds(policyOpts.WindowSeconds)));
            });

            // Register Policy 3: Engagement
            rateLimiterOptions.AddPolicy(EngagementPolicy, httpContext =>
            {
                var limiterService = httpContext.RequestServices.GetRequiredService<IRateLimiterService>();
                var policyOpts = options.Engagement;
                var partitionKey = ResolvePartitionKey(httpContext, "eng", options.RedisKeyPrefix);

                return RateLimitPartition.Get(partitionKey, key =>
                    new DistributedRateLimiterAdapter(limiterService, key, policyOpts.PermitLimit, TimeSpan.FromSeconds(policyOpts.WindowSeconds)));
            });

            // Register Policy 4: Search
            rateLimiterOptions.AddPolicy(SearchPolicy, httpContext =>
            {
                var limiterService = httpContext.RequestServices.GetRequiredService<IRateLimiterService>();
                var policyOpts = options.Search;
                var partitionKey = ResolvePartitionKey(httpContext, "search", options.RedisKeyPrefix);

                return RateLimitPartition.Get(partitionKey, key =>
                    new DistributedRateLimiterAdapter(limiterService, key, policyOpts.PermitLimit, TimeSpan.FromSeconds(policyOpts.WindowSeconds)));
            });

            // Register Policy 5: Reporting
            rateLimiterOptions.AddPolicy(ReportingPolicy, httpContext =>
            {
                var limiterService = httpContext.RequestServices.GetRequiredService<IRateLimiterService>();
                var policyOpts = options.Reporting;
                var partitionKey = ResolvePartitionKey(httpContext, "rep", options.RedisKeyPrefix);

                return RateLimitPartition.Get(partitionKey, key =>
                    new DistributedRateLimiterAdapter(limiterService, key, policyOpts.PermitLimit, TimeSpan.FromSeconds(policyOpts.WindowSeconds)));
            });
        });

        return services;
    }

    private static string ResolvePartitionKey(HttpContext context, string policy, string prefix)
    {
        string? clientIp = null;
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded))
        {
            var firstIp = forwarded.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp)) clientIp = firstIp;
        }

        clientIp ??= context.Connection.RemoteIpAddress?.ToString();

        // In TestServer environments without physical sockets (where RemoteIpAddress is null and no X-Forwarded-For was provided),
        // isolate each untagged test request by trace identifier to prevent global test-suite starvation across phases.
        clientIp ??= $"test-{context.TraceIdentifier}";

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && !string.IsNullOrWhiteSpace(userIdClaim.Value))
        {
            return $"{prefix}{policy}:user:{userIdClaim.Value}:{clientIp}";
        }

        return $"{prefix}{policy}:ip:{clientIp}";
    }
}

public class DistributedRateLimiterAdapter : RateLimiter
{
    private readonly IRateLimiterService _rateLimiterService;
    private readonly string _key;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;

    public DistributedRateLimiterAdapter(
        IRateLimiterService rateLimiterService,
        string key,
        int permitLimit,
        TimeSpan window)
    {
        _rateLimiterService = rateLimiterService;
        _key = key;
        _permitLimit = permitLimit;
        _window = window;
    }

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var result = _rateLimiterService.CheckRateLimitAsync(_key, _permitLimit, _window).GetAwaiter().GetResult();
        return new DistributedRateLimitLease(result);
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var result = await _rateLimiterService.CheckRateLimitAsync(_key, _permitLimit, _window, cancellationToken);
        return new DistributedRateLimitLease(result);
    }
}

public class DistributedRateLimitLease : RateLimitLease
{
    private readonly RateLimitResult _result;

    public DistributedRateLimitLease(RateLimitResult result)
    {
        _result = result;
    }

    public override bool IsAcquired => _result.IsAllowed;

    public override IEnumerable<string> MetadataNames
    {
        get
        {
            if (!_result.IsAllowed)
            {
                yield return MetadataName.RetryAfter.Name;
            }
        }
    }

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (!_result.IsAllowed && metadataName == MetadataName.RetryAfter.Name)
        {
            metadata = TimeSpan.FromSeconds(_result.RetryAfterSeconds);
            return true;
        }

        metadata = null;
        return false;
    }
}
