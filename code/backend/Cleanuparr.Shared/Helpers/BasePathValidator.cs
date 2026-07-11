using System.Text.RegularExpressions;

namespace Cleanuparr.Shared.Helpers;

/// <summary>
/// Validates BASE_PATH values to ensure security and proper formatting
/// </summary>
public static class BasePathValidator
{
    private const int MaxLength = 100;
    private static readonly Regex ValidPathRegex = new(@"^/[a-zA-Z0-9_\-]+(/[a-zA-Z0-9_\-]+)*$", RegexOptions.Compiled);

    /// <summary>
    /// Validates a BASE_PATH value
    /// </summary>
    /// <param name="basePath">The base path to validate</param>
    /// <returns>Tuple with validity flag and error message if invalid</returns>
    public static (bool IsValid, string ErrorMessage) Validate(string? basePath)
    {
        // Empty or null is valid (no base path)
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return (true, string.Empty);
        }

        // Trim whitespace
        basePath = basePath.Trim();

        // Check for just root path
        if (basePath == "/")
        {
            return (false, "BASE_PATH cannot be just '/' (conflicts with root)");
        }

        // Check length
        if (basePath.Length > MaxLength)
        {
            return (false, $"BASE_PATH cannot be longer than {MaxLength} characters");
        }

        // Must start with /
        if (!basePath.StartsWith('/'))
        {
            return (false, "BASE_PATH must start with '/'");
        }

        // No dots allowed (prevents path traversal)
        if (basePath.Contains('.'))
        {
            return (false, "BASE_PATH cannot contain dots (.) for security reasons");
        }

        // No double slashes
        if (basePath.Contains("//"))
        {
            return (false, "BASE_PATH cannot contain double slashes (//)");
        }

        // Must not end with slash (except root)
        if (basePath.EndsWith('/') && basePath != "/")
        {
            return (false, "BASE_PATH cannot end with '/' (except root)");
        }

        // Validate format using regex (alphanumeric, hyphens, underscores only)
        if (!ValidPathRegex.IsMatch(basePath))
        {
            return (false, "BASE_PATH can only contain letters, numbers, hyphens (-), and underscores (_)");
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Normalizes a BASE_PATH value (removes trailing slash, trims whitespace)
    /// </summary>
    /// <param name="basePath">The base path to normalize</param>
    /// <returns>Normalized base path or empty string if invalid</returns>
    public static string Normalize(string? basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return string.Empty;
        }

        basePath = basePath.Trim();

        // Remove trailing slash unless it's just "/"
        if (basePath.EndsWith('/') && basePath != "/")
        {
            basePath = basePath.TrimEnd('/');
        }

        return basePath;
    }
}
