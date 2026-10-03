using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

/// <summary>
/// Embed sent in a Discord webhook message.
/// </summary>
public class DiscordEmbed
{
    /// <summary>
    /// Title of the embed.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Main content of the embed.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Hex color of the embed sidebar.
    /// </summary>
    public int Color { get; set; }

    /// <summary>
    /// Small thumbnail image for the embed.
    /// </summary>
    public DiscordThumbnail? Thumbnail { get; set; }

    /// <summary>
    /// Large image for the embed.
    /// </summary>
    public DiscordImage? Image { get; set; }

    /// <summary>
    /// Fields displayed in the embed body.
    /// </summary>
    public List<DiscordField> Fields { get; set; } = new();

    /// <summary>
    /// Footer section of the embed.
    /// </summary>
    public DiscordFooter? Footer { get; set; }

    /// <summary>
    /// ISO 8601 timestamp displayed in the embed footer.
    /// </summary>
    public string? Timestamp { get; set; }
}
