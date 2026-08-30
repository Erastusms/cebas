using System.Text.RegularExpressions;

namespace CEBAS.Domain.Common;

public static class IdentityNormalizers
{
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_]{3,30}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Normalizes a username for comparison and unique constraint lookups (trimmed, lowercase).
    /// </summary>
    public static string NormalizeUsername(string? username)
    {
        return username?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Validates username format: 3-30 alphanumeric characters and underscores only.
    /// </summary>
    public static bool IsValidUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        return UsernameRegex.IsMatch(username);
    }

    /// <summary>
    /// Normalizes an email address for comparison and unique constraint lookups (trimmed, lowercase).
    /// </summary>
    public static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Validates email format using RFC-compliant pattern.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        if (email.Length > 255) return false;
        return EmailRegex.IsMatch(email.Trim());
    }
}
