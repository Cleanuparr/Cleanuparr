using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Infrastructure.Verticals.Notifications;
using Mapster;

namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public class NotifiarrProvider : NotificationProvider<NotifiarrConfig>
{
    private readonly DataContext _dataContext;
    private readonly INotifiarrProxy _proxy;

    private const string WarningColor = "f0ad4e";
    private const string ImportantColor = "bb2124";
    private const string Logo = "https://github.com/Cleanuparr/Cleanuparr/blob/main/Logo/48.png?raw=true";

    public override string Name => "Notifiarr";

    public NotifiarrProvider(DataContext dataContext, INotifiarrProxy proxy)
        : base(dataContext.NotifiarrConfigs)
    {
        _dataContext = dataContext;
        _proxy = proxy;
    }

    public override async Task OnFailedImportStrike(FailedImportStrikeNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, WarningColor), Config);
    }
    
    public override async Task OnStalledStrike(StalledStrikeNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, WarningColor), Config);
    }
    
    public override async Task OnSlowStrike(SlowStrikeNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, WarningColor), Config);
    }
    
    public override async Task OnQueueItemDeleted(QueueItemDeletedNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification, ImportantColor), Config);
    }

    public override async Task OnDownloadCleaned(DownloadCleanedNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification), Config);
    }
    
    public override async Task OnCategoryChanged(CategoryChangedNotification notification)
    {
        await _proxy.SendNotification(BuildPayload(notification), Config);
    }

    private NotifiarrPayload BuildPayload(ArrNotification notification, string color)
    {
        NotifiarrPayload payload = new()
        {
            Discord = new()
            {
                Color = color,
                Text = new()
                {
                    Title = notification.Title,
                    Icon = Logo,
                    Description = notification.Description,
                    Fields = new()
                    {
                        new() { Title = "Instance type", Text = notification.InstanceType.ToString() },
                        new() { Title = "Url", Text = notification.InstanceUrl.ToString() },
                        new() { Title = "Download hash", Text = notification.Hash }
                    }
                },
                Ids = new Ids
                {
                    Channel = Config.ChannelId
                },
                Images = new()
                {
                    Thumbnail = new Uri(Logo),
                    Image = notification.Image
                }
            }
        };
        
        payload.Discord.Text.Fields.AddRange(notification.Fields?.Adapt<List<Field>>() ?? []);

        return payload;
    }

    private NotifiarrPayload BuildPayload(DownloadCleanedNotification notification)
    {
        NotifiarrPayload payload = new()
        {
            Discord = new()
            {
                Color = ImportantColor,
                Text = new()
                {
                    Title = notification.Title,
                    Icon = Logo,
                    Description = notification.Description,
                    Fields = notification.Fields?.Adapt<List<Field>>() ?? []
                },
                Ids = new Ids
                {
                    Channel = Config.ChannelId
                },
                Images = new()
                {
                    Thumbnail = new Uri(Logo)
                }
            }
        };
        
        return payload;
    }

    private NotifiarrPayload BuildPayload(CategoryChangedNotification notification)
    {
        NotifiarrPayload payload = new()
        {
            Discord = new()
            {
                Color = WarningColor,
                Text = new()
                {
                    Title = notification.Title,
                    Icon = Logo,
                    Description = notification.Description,
                    Fields = notification.Fields?.Adapt<List<Field>>() ?? []
                },
                Ids = new Ids
                {
                    Channel = Config.ChannelId
                },
                Images = new()
                {
                    Thumbnail = new Uri(Logo)
                }
            }
        };
        
        return payload;
    }
}