namespace Cleanuparr.Shared.Attributes;

/// <summary>
/// Defines how sensitive data should be masked in API responses.
/// </summary>
public enum SensitiveDataType
{
    /// <summary>
    /// Full mask: replaces the entire value with bullets (••••••••).
    /// Use for passwords, API keys, tokens, webhook URLs.
    /// </summary>
    Full,

    /// <summary>
    /// Apprise URL mask: shows only the scheme of each service URL (discord://••••••••).
    /// Use for Apprise service URL strings that contain multiple notification service URLs.
    /// </summary>
    AppriseUrl,
}
