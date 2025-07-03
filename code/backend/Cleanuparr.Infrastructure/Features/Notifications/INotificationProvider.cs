using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public interface INotificationProvider<T> : INotificationProvider
    where T : NotificationConfig
{
    new T Config { get; }
}

public interface INotificationProvider
{
    NotificationConfig Config { get; }
    
    string Name { get; }
    
    Task OnFailedImportStrike(FailedImportStrikeNotification notification);
        
    Task OnStalledStrike(StalledStrikeNotification notification);
    
    Task OnSlowStrike(SlowStrikeNotification notification);

    Task OnQueueItemDeleted(QueueItemDeletedNotification notification);

    Task OnDownloadCleaned(DownloadCleanedNotification notification);
    
    Task OnCategoryChanged(CategoryChangedNotification notification);
}