using System.Text;
using System.Text.Json;

namespace CEBAS.Application.Common;

/// <summary>
/// Keyset Cursor-Based pagination result contract (ADR-03).
/// Eliminates OFFSET/LIMIT degradation and prevents duplicate or omitted items.
/// </summary>
public class CursorPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasNextPage { get; init; }
    public int PageSize { get; init; }

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
/// Encapsulates a keyset cursor (CreatedAt timestamp + UUIDv7 Id) encoded as Base64.
/// </summary>
public record Cursor(DateTimeOffset CreatedAt, Guid Id)
{
    public string Encode()
    {
        var payload = JsonSerializer.Serialize(new { c = CreatedAt.ToUnixTimeMilliseconds(), i = Id });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static Cursor? Decode(string? cursorString)
    {
        if (string.IsNullOrWhiteSpace(cursorString)) return null;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursorString));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("c", out var cProp) && root.TryGetProperty("i", out var iProp))
            {
                long ms = cProp.GetInt64();
                Guid id = iProp.GetGuid();
                return new Cursor(DateTimeOffset.FromUnixTimeMilliseconds(ms), id);
            }
        }
        catch
        {
            // Invalid or corrupt cursor string
        }

        return null;
    }
}
