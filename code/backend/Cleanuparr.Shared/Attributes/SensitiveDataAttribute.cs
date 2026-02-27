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

/// <summary>
/// Marks a property as containing sensitive data that should be masked in API responses
/// and preserved when the placeholder value is sent back in updates.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SensitiveDataAttribute : Attribute
{
    public SensitiveDataType Type { get; }

    public SensitiveDataAttribute(SensitiveDataType type = SensitiveDataType.Full)
    {
        Type = type;
    }
}
