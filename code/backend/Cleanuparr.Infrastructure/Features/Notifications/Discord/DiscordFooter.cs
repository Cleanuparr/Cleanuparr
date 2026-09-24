using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public class DiscordFooter
{
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }
}
