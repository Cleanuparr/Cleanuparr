using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

public sealed class GotifyProvider : NotificationProviderBase<GotifyConfig>
{
    private readonly IGotifyProxy _proxy;

    public GotifyProvider(
        string name,
        NotificationProviderType type,
        GotifyConfig config,
        IGotifyProxy proxy)
        : base(name, type, config)
    {
        _proxy = proxy;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        var payload = BuildPayload(context);
        await _proxy.SendNotification(payload, Config);
    }

    private GotifyPayload BuildPayload(NotificationContext context)
    {
        var message = BuildMessage(context);

        return new GotifyPayload
        {
            Title = context.Title,
            Message = message,
            Priority = Config.Priority,
            Extras = new GotifyExtras
            {
                ClientDisplay = new GotifyClientDisplay
                {
                    ContentType = "text/markdown"
                }
            }
        };
    }

    private string BuildMessage(NotificationContext context)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(context.Description))
        {
            lines.Add(context.Description);
        }

        foreach ((string key, string value) in context.Data)
        {
            lines.Add($"**{key}:** {value}");
        }

        return string.Join("\n\n", lines);
    }
}
