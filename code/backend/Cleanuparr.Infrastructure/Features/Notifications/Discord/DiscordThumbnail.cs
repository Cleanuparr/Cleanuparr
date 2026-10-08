namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

/// <summary>
/// Thumbnail image displayed in a Discord embed.
/// </summary>
public class DiscordThumbnail
{
    /// <summary>
    /// URL of the thumbnail image.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
