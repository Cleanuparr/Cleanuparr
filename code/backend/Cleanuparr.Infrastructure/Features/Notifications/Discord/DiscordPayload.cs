using Newtonsoft.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public class DiscordPayload
{
    public string? Username { get; set; }

    [JsonProperty("avatar_url")]
    public string? AvatarUrl { get; set; }

    public List<DiscordEmbed> Embeds { get; set; } = new();
}

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

public class DiscordField
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public bool Inline { get; set; }
}

public class DiscordThumbnail
{
    public string Url { get; set; } = string.Empty;
}

public class DiscordImage
{
    public string Url { get; set; } = string.Empty;
}

public class DiscordFooter
{
    public string Text { get; set; } = string.Empty;

    [JsonProperty("icon_url")]
    public string? IconUrl { get; set; }
}
