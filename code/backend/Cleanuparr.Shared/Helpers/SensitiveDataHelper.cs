using System;
using System.Text.RegularExpressions;

namespace Cleanuparr.Shared.Helpers;

/// <summary>
/// Helpers for sensitive data masking in API responses and request handling.
/// </summary>
public static partial class SensitiveDataHelper
{
    /// <summary>
    /// The placeholder string used to mask sensitive data in API responses.
    /// When this value is detected in an update request, the existing DB value is preserved.
    /// </summary>
    public const string Placeholder = "••••••••";

    /// <summary>
    /// Returns true if the given value contains the sensitive data placeholder.
    /// Uses Contains (not Equals) to handle Apprise URLs like "discord://••••••••".
    /// </summary>
    public static bool IsPlaceholder(this string? value)
        => value is not null && value.Contains(Placeholder, StringComparison.Ordinal);

    /// <summary>
    /// Masks Apprise service URLs by preserving only the scheme.
    /// Input:  "discord://token slack://tokenA/tokenB"
    /// Output: "discord://•••••••• slack://••••••••"
    /// </summary>
    public static string MaskAppriseUrls(string serviceUrls)
    {
        if (string.IsNullOrWhiteSpace(serviceUrls))
        {
            return serviceUrls;
        }

        return AppriseUrlPattern().Replace(serviceUrls, match =>
        {
            var scheme = match.Groups[1].Value;
            return $"{scheme}://{Placeholder}";
        });
    }

    [GeneratedRegex(@"([a-zA-Z][a-zA-Z0-9+.\-]*)://\S+")]
    private static partial Regex AppriseUrlPattern();
}
