using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace CEBAS.Infrastructure.Observability;

/// <summary>
/// Serilog enricher and sanitizer that detects and redacts sensitive credentials, tokens,
/// passwords, session cookies, and authentication headers before logs are written or forwarded.
/// </summary>
public partial class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> SensitiveKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwd",
        "secret",
        "token",
        "accessToken",
        "refreshToken",
        "sessionToken",
        "authorization",
        "cookie",
        "apiKey",
        "apiSecret",
        "privateKey",
        "connectionString"
    };

    [GeneratedRegex(@"(password|token|secret|authorization|cookie)\s*[:=]\s*[""']?([^""'\s;,]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SensitiveTextRegex();

    public const string RedactedPlaceholder = "***REDACTED***";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToUpdate = new List<KeyValuePair<string, LogEventPropertyValue>>();

        foreach (var property in logEvent.Properties)
        {
            if (IsSensitiveKey(property.Key))
            {
                propertiesToUpdate.Add(new KeyValuePair<string, LogEventPropertyValue>(
                    property.Key,
                    new ScalarValue(RedactedPlaceholder)));
            }
            else if (property.Value is ScalarValue scalar && scalar.Value is string strVal)
            {
                if (ContainsSensitivePatterns(property.Key, strVal))
                {
                    var masked = MaskSensitiveText(strVal);
                    propertiesToUpdate.Add(new KeyValuePair<string, LogEventPropertyValue>(
                        property.Key,
                        new ScalarValue(masked)));
                }
            }
            else if (property.Value is StructureValue structure)
            {
                var sanitized = SanitizeStructure(structure);
                if (sanitized != structure)
                {
                    propertiesToUpdate.Add(new KeyValuePair<string, LogEventPropertyValue>(
                        property.Key,
                        sanitized));
                }
            }
        }

        foreach (var (key, value) in propertiesToUpdate)
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty(key, value));
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (SensitiveKeyNames.Contains(key)) return true;
        foreach (var sensitive in SensitiveKeyNames)
        {
            if (key.IndexOf(sensitive, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsSensitivePatterns(string key, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return true;
        return SensitiveTextRegex().IsMatch(text);
    }

    public static string MaskSensitiveText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        if (input.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return $"Bearer {RedactedPlaceholder}";
        }
        return SensitiveTextRegex().Replace(input, match =>
        {
            var keyGroup = match.Groups[1].Value;
            return $"{keyGroup}={RedactedPlaceholder}";
        });
    }

    private static StructureValue SanitizeStructure(StructureValue structure)
    {
        var properties = new List<LogEventProperty>();
        bool modified = false;

        foreach (var prop in structure.Properties)
        {
            if (IsSensitiveKey(prop.Name))
            {
                properties.Add(new LogEventProperty(prop.Name, new ScalarValue(RedactedPlaceholder)));
                modified = true;
            }
            else if (prop.Value is StructureValue nested)
            {
                var sanitizedNested = SanitizeStructure(nested);
                properties.Add(new LogEventProperty(prop.Name, sanitizedNested));
                if (sanitizedNested != nested) modified = true;
            }
            else
            {
                properties.Add(prop);
            }
        }

        return modified ? new StructureValue(properties, structure.TypeTag) : structure;
    }
}
