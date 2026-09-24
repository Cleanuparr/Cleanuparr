namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public class DiscordField
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public bool Inline { get; set; }
}
