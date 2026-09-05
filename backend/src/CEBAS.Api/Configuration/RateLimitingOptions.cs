namespace CEBAS.Api.Configuration;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public string RedisKeyPrefix { get; set; } = "cebas:rl:";
    public PolicyLimitOptions Authentication { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public PolicyLimitOptions Publishing { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
    public PolicyLimitOptions Engagement { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public PolicyLimitOptions Search { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
    public PolicyLimitOptions Reporting { get; set; } = new() { PermitLimit = 10, WindowSeconds = 300 };

    public PolicyLimitOptions GetPolicyOptions(string policyName) => policyName.ToLowerInvariant() switch
    {
        "authentication" => Authentication,
        "publishing" => Publishing,
        "engagement" => Engagement,
        "search" => Search,
        "reporting" => Reporting,
        _ => new PolicyLimitOptions { PermitLimit = 60, WindowSeconds = 60 }
    };
}

public class PolicyLimitOptions
{
    public int PermitLimit { get; set; } = 30;
    public int WindowSeconds { get; set; } = 60;
}
