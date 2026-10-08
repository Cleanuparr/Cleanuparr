using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public class DiscordPayload
{
    public string? Username { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    public List<DiscordEmbed> Embeds { get; set; } = new();
}
