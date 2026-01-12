using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public sealed class DiscordProvider : NotificationProviderBase<DiscordConfig>
{
    private readonly IDiscordProxy _proxy;

    public DiscordProvider(
        string name,
        NotificationProviderType type,
        DiscordConfig config,
        IDiscordProxy proxy)
        : base(name, type, config)
    {
        _proxy = proxy;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        var payload = BuildPayload(context);
        await _proxy.SendNotification(payload, Config);
    }

    private DiscordPayload BuildPayload(NotificationContext context)
    {
        var color = context.Severity switch
        {
            EventSeverity.Warning => 0xf0ad4e,   // Orange/yellow
            EventSeverity.Important => 0xbb2124, // Red
            _ => 0x28a745                        // Green
        };

        var embed = new DiscordEmbed
        {
            Title = context.Title,
            Description = context.Description,
            Color = color,
            Thumbnail = new DiscordThumbnail { Url = Constants.LogoUrl },
            Fields = BuildFields(context),
            Footer = new DiscordFooter
            {
                Text = "Cleanuparr",
                IconUrl = Constants.LogoUrl
            },
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        if (context.Image != null)
        {
            embed.Image = new DiscordImage { Url = context.Image.ToString() };
        }

        var payload = new DiscordPayload
        {
            Embeds = new List<DiscordEmbed> { embed }
        };

        // Apply username override if configured
        if (!string.IsNullOrWhiteSpace(Config.Username))
        {
            payload.Username = Config.Username;
        }

        // Apply avatar override if configured
        if (!string.IsNullOrWhiteSpace(Config.AvatarUrl))
        {
            payload.AvatarUrl = Config.AvatarUrl;
        }

        return payload;
    }

    private List<DiscordField> BuildFields(NotificationContext context)
    {
        var fields = new List<DiscordField>();

        foreach ((string key, string value) in context.Data)
        {
            fields.Add(new DiscordField
            {
                Name = key,
                Value = value,
                Inline = false
            });
        }

        return fields;
    }
}
