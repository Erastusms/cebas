namespace CEBAS.Application.Abstractions;

public sealed record ExtractedHashtag(string NormalizedName, string DisplayName);

public interface IHashtagParser
{
    /// <summary>
    /// Deterministically extracts distinct hashtags from post text content.
    /// Deduplicates tags within the post while preserving the first encountered display name.
    /// </summary>
    IReadOnlyList<ExtractedHashtag> ExtractHashtags(string? content);

    /// <summary>
    /// Normalizes a hashtag string to its canonical lower-case representation without leading '#'.
    /// </summary>
    string Normalize(string tag);
}
