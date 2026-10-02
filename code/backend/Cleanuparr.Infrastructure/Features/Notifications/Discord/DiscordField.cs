namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

/// <summary>
/// Field displayed in a Discord embed.
/// </summary>
public class DiscordField
{
    /// <summary>
    /// Field name or label.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field content or value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the field displays inline with other fields.
    /// </summary>
    public bool Inline { get; set; }
}
