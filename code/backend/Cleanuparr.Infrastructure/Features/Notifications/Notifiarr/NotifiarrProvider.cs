using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public sealed class NotifiarrProvider : NotificationProviderBase<NotifiarrConfig>
{
    private readonly INotifiarrProxy _proxy;

    public NotifiarrProvider(
        string name,
        NotificationProviderType type,
        NotifiarrConfig config,
        INotifiarrProxy proxy)
        : base(name, type, config)
    {
        _proxy = proxy;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        var payload = BuildPayload(context);
        await _proxy.SendNotification(payload, Config);
    }

    private NotifiarrPayload BuildPayload(NotificationContext context)
    {
        var color = context.Severity switch
        {
            EventSeverity.Warning => "f0ad4e",
            EventSeverity.Important => "bb2124",
            _ => "28a745"
        };

        const string logo = "https://github.com/Cleanuparr/Cleanuparr/blob/main/Logo/48.png?raw=true";

        return new NotifiarrPayload
        {
            Discord = new()
            {
                Color = color,
                Text = new()
                {
                    Title = context.Title,
                    Icon = logo,
                    Description = context.Description,
                    Fields = BuildFields(context)
                },
                Ids = new Ids
                {
                    Channel = Config.ChannelId
                },
                Images = new()
                {
                    Thumbnail = new Uri(logo)
                }
            }
        };
    }

    private List<Field> BuildFields(NotificationContext context)
    {
        var fields = new List<Field>();

        if (context.Data.TryGetValue("instanceType", out var instanceType) && instanceType != null)
        {
            fields.Add(new Field { Title = "Instance type", Text = instanceType.ToString() ?? string.Empty });
        }

        if (context.Data.TryGetValue("instanceUrl", out var instanceUrl) && instanceUrl != null)
        {
            fields.Add(new Field { Title = "Url", Text = instanceUrl.ToString() ?? string.Empty });
        }

        if (context.Data.TryGetValue("hash", out var hash) && hash != null)
        {
            fields.Add(new Field { Title = "Download hash", Text = hash.ToString() ?? string.Empty });
        }

        return fields;
    }
}
