using System.Text.RegularExpressions;
using CEBAS.Application.Abstractions;

namespace CEBAS.Api.Features.Hashtags.ExtractHashtags;

/// <summary>
/// High-performance deterministic hashtag parser for CEBAS.
/// Supports alphanumeric hashtags with underscores (#hashtag, #CEBAS, #hello_world, #topic123).
/// Enforces boundary invariants, prevents duplicate extraction per post, and ensures canonical normalization.
/// </summary>
public partial class HashtagParser : IHashtagParser
{
    // Regex explanation:
    // (?<![#\p{L}\p{N}_@]) : Negative lookbehind ensures # is not preceded by #, letters, numbers, underscore, or @
    // #                     : Matches the hashtag symbol
    // ([a-zA-Z0-9_]+)       : Captures one or more alphanumeric characters or underscores
    [GeneratedRegex(@"(?<![#\p{L}\p{N}_@])#([a-zA-Z0-9_]+)", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HashtagRegex();

    public IReadOnlyList<ExtractedHashtag> ExtractHashtags(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<ExtractedHashtag>();
        }

        var matches = HashtagRegex().Matches(content);
        if (matches.Count == 0)
        {
            return Array.Empty<ExtractedHashtag>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ExtractedHashtag>(matches.Count);

        foreach (Match match in matches)
        {
            if (!match.Success || match.Groups.Count < 2) continue;

            var rawTag = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(rawTag)) continue;

            var normalized = Normalize(rawTag);
            if (seen.Add(normalized))
            {
                results.Add(new ExtractedHashtag(normalized, rawTag));
            }
        }

        return results;
    }

    public string Normalize(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        return tag.Trim().TrimStart('#').Trim().ToLowerInvariant();
    }
}
