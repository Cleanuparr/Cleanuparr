using Common.Configuration.Notification;
using Infrastructure.Verticals.Notifications.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.Verticals.Notifications;

public abstract class NotificationProvider : INotificationProvider
{
    protected NotificationProvider(IOptions<NotificationConfig> config)
    {
        Config = config.Value;
    }
    
    public abstract string Name { get; }
    
    public NotificationConfig Config { get; }
    
    public abstract Task OnFailedImportStrike(FailedImportStrikeNotification notification);

    public abstract Task OnStalledStrike(StalledStrikeNotification notification);
    
    public abstract Task OnSlowStrike(SlowStrikeNotification notification);

    public abstract Task OnQueueItemDeleted(QueueItemDeletedNotification notification);

    public abstract Task OnDownloadCleaned(DownloadCleanedNotification notification);
    
    public abstract Task OnCategoryChanged(CategoryChangedNotification notification);
}