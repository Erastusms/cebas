using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a distinct social hashtag.
/// Stores both the canonical normalized name (lowercase for indexing/aggregation)
/// and the display name (original casing as first encountered).
/// </summary>
public class Hashtag : Entity
{
    public const int MaxNameLength = 100;

    public string NormalizedName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;

    // Navigation collection
    public ICollection<PostHashtag> PostHashtags { get; private set; } = new List<PostHashtag>();

    // EF Core parameterless constructor
    protected Hashtag() { }

    public static Hashtag Create(string normalizedName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ValidationException("NormalizedName", "Hashtag normalized name cannot be empty.");
        }

        var trimmedNorm = normalizedName.Trim().ToLowerInvariant();
        if (trimmedNorm.Length > MaxNameLength)
        {
            throw new ValidationException("NormalizedName", $"Hashtag name cannot exceed {MaxNameLength} characters.");
        }

        var trimmedDisplay = string.IsNullOrWhiteSpace(displayName) ? trimmedNorm : displayName.Trim();
        if (trimmedDisplay.Length > MaxNameLength)
        {
            trimmedDisplay = trimmedDisplay[..MaxNameLength];
        }

        return new Hashtag
        {
            Id = Uuid7.New(),
            NormalizedName = trimmedNorm,
            DisplayName = trimmedDisplay,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var trimmed = displayName.Trim();
            DisplayName = trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
