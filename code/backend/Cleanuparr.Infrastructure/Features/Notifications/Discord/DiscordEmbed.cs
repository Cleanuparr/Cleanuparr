using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public class DiscordEmbed
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Color { get; set; }

    public DiscordThumbnail? Thumbnail { get; set; }

    public DiscordImage? Image { get; set; }

    public List<DiscordField> Fields { get; set; } = new();

    public DiscordFooter? Footer { get; set; }

    public string? Timestamp { get; set; }
}
