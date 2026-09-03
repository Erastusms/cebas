using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CEBAS.Application.Common;

/// <summary>
/// Keyset Cursor-Based pagination result contract (ADR-03 / Phase 6).
/// Eliminates OFFSET/LIMIT degradation and prevents duplicate or omitted items.
/// </summary>
public class CursorPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasNextPage { get; init; }
    public int PageSize { get; init; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursorSnake => NextCursor;

    [JsonPropertyName("has_next_page")]
    public bool HasNextPageSnake => HasNextPage;

    public static CursorPagedResult<T> Create(IReadOnlyList<T> items, int pageSize, Func<T, Cursor> cursorSelector)
    {
        bool hasNext = items.Count > pageSize;
        var resultItems = hasNext ? items.Take(pageSize).ToList() : items;
        string? nextCursor = null;

        if (hasNext && resultItems.Count > 0)
        {
            var lastItem = resultItems[^1];
            nextCursor = cursorSelector(lastItem).Encode();
        }

        return new CursorPagedResult<T>
        {
            Items = resultItems,
            NextCursor = nextCursor,
            HasNextPage = hasNext,
            PageSize = pageSize
        };
    }
}

/// <summary>
/// Encapsulates a keyset cursor (CreatedAt timestamp + UUIDv7 Id) encoded as an opaque Base64 token.
/// Supports microsecond/tick resolution and backward-compatible millisecond payloads.
/// </summary>
public record Cursor(DateTimeOffset CreatedAt, Guid Id)
{
    private static readonly DateTimeOffset MinAllowedTime = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public string Encode()
    {
        var payload = JsonSerializer.Serialize(new
        {
            c = CreatedAt.ToUnixTimeMilliseconds(),
            u = CreatedAt.Ticks,
            i = Id
        });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Decodes a Base64-encoded cursor token. Returns null if invalid or absent.
    /// </summary>
    public static Cursor? Decode(string? cursorString)
    {
        return TryDecode(cursorString, out var cursor, out _) ? cursor : null;
    }

    /// <summary>
    /// Strictly validates and decodes a cursor token.
    /// Returns true with cursor = null when cursorString is null or whitespace (first page).
    /// Returns false with errorMessage when cursorString is malformed, corrupted, or has invalid data.
    /// </summary>
    public static bool TryDecode(string? cursorString, out Cursor? cursor, out string? errorMessage)
    {
        cursor = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(cursorString))
        {
            return true;
        }

        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(cursorString.Trim());
        }
        catch (FormatException)
        {
            errorMessage = "Cursor must be a valid Base64-encoded string.";
            return false;
        }

        string json;
        try
        {
            json = Encoding.UTF8.GetString(rawBytes);
        }
        catch
        {
            errorMessage = "Cursor payload could not be decoded as UTF-8.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Cursor payload must be a JSON object.";
                return false;
            }

            if (!root.TryGetProperty("i", out var iProp) || !iProp.TryGetGuid(out var id) || id == Guid.Empty)
            {
                errorMessage = "Cursor contains an invalid or empty identifier.";
                return false;
            }

            DateTimeOffset timestamp;
            if (root.TryGetProperty("u", out var uProp) && uProp.TryGetInt64(out var ticks))
            {
                if (ticks < MinAllowedTime.Ticks)
                {
                    errorMessage = "Cursor timestamp is outside acceptable bounds.";
                    return false;
                }
                timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            }
            else if (root.TryGetProperty("c", out var cProp) && cProp.TryGetInt64(out var ms))
            {
                if (ms < MinAllowedTime.ToUnixTimeMilliseconds())
                {
                    errorMessage = "Cursor timestamp is outside acceptable bounds.";
                    return false;
                }
                timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            }
            else
            {
                errorMessage = "Cursor is missing a valid timestamp.";
                return false;
            }

            if (timestamp > DateTimeOffset.UtcNow.AddDays(1))
            {
                errorMessage = "Cursor timestamp cannot be in the future.";
                return false;
            }

            cursor = new Cursor(timestamp, id);
            return true;
        }
        catch (JsonException)
        {
            errorMessage = "Cursor payload is corrupted or contains malformed JSON.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Cursor validation failed: {ex.Message}";
            return false;
        }
    }
}
