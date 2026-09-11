using System.Text;
using System.Text.Json;

namespace CEBAS.Infrastructure.Search;

/// <summary>
/// Opaque Base64-encoded cursor for deterministic Elasticsearch search_after pagination.
/// Contains (Score, CreatedAtMs, Id) tuple corresponding to the multi-field sort criteria.
/// </summary>
public record SearchCursor(double Score, long CreatedAtMs, string Id)
{
    public string Encode()
    {
        var payload = JsonSerializer.Serialize(new
        {
            s = Math.Round(Score, 6),
            c = CreatedAtMs,
            i = Id
        });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string? cursorString, out SearchCursor? cursor, out string? errorMessage)
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
            errorMessage = "Cursor must be a valid Base64 string.";
            return false;
        }

        string json;
        try
        {
            json = Encoding.UTF8.GetString(rawBytes);
        }
        catch
        {
            errorMessage = "Cursor could not be decoded as UTF-8.";
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

            if (!root.TryGetProperty("i", out var iProp) || string.IsNullOrWhiteSpace(iProp.GetString()))
            {
                errorMessage = "Cursor is missing a valid document identifier.";
                return false;
            }

            var id = iProp.GetString()!;

            double score = 0;
            if (root.TryGetProperty("s", out var sProp) && sProp.TryGetDouble(out var parsedScore))
            {
                score = parsedScore;
            }

            long createdAtMs = 0;
            if (root.TryGetProperty("c", out var cProp) && cProp.TryGetInt64(out var parsedMs))
            {
                createdAtMs = parsedMs;
            }

            cursor = new SearchCursor(score, createdAtMs, id);
            return true;
        }
        catch (JsonException)
        {
            errorMessage = "Cursor payload is corrupted or malformed.";
            return false;
        }
    }
}
