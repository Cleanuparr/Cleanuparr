using Common.Configuration.Notification;
using Infrastructure.Verticals.Notifications.Models;

namespace Infrastructure.Verticals.Notifications;

public interface INotificationProvider
{
    BaseNotificationConfig Config { get; }
    
    string Name { get; }
    
    Task OnFailedImportStrike(FailedImportStrikeNotification notification);
        
    Task OnStalledStrike(StalledStrikeNotification notification);
    
    Task OnSlowStrike(SlowStrikeNotification notification);

    Task OnQueueItemDeleted(QueueItemDeletedNotification notification);

    Task OnDownloadCleaned(DownloadCleanedNotification notification);
    
    Task OnCategoryChanged(CategoryChangedNotification notification);
}