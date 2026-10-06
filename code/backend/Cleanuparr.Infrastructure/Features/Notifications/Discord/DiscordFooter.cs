using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

/// <summary>
/// Footer section of a Discord embed.
/// </summary>
public class DiscordFooter
{
    /// <summary>
    /// Footer text displayed below the embed.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// URL of a small icon shown in the footer.
    /// </summary>
    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }
}
