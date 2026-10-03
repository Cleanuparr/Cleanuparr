namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

/// <summary>
/// Large image displayed in a Discord embed.
/// </summary>
public class DiscordImage
{
    /// <summary>
    /// URL of the image to display.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
