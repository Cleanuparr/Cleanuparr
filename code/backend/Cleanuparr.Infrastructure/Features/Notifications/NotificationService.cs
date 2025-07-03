using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Verticals.Notifications;

public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly INotificationFactory _notificationFactory;

    public NotificationService(ILogger<NotificationService> logger, INotificationFactory notificationFactory)
    {
        _logger = logger;
        _notificationFactory = notificationFactory;
    }

    public async Task Notify(FailedImportStrikeNotification notification)
    {
        foreach (INotificationProvider provider in _notificationFactory.OnFailedImportStrikeEnabled())
        {
            try
            {
                await provider.OnFailedImportStrike(notification);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "failed to send notification | provider {provider}", provider.Name);
            }
        }
    }
    
    public async Task Notify(StalledStrikeNotification notification)
    {
        foreach (INotificationProvider provider in _notificationFactory.OnStalledStrikeEnabled())
        {
            try
            {
                await provider.OnStalledStrike(notification);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "failed to send notification | provider {provider}", provider.Name);
            }
        }
    }
    
    public async Task Notify(SlowStrikeNotification notification)
    {
        foreach (INotificationProvider provider in _notificationFactory.OnSlowStrikeEnabled())
        {
            try
            {
                await provider.OnSlowStrike(notification);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "failed to send notification | provider {provider}", provider.Name);
            }
        }
    }
    
    public async Task Notify(QueueItemDeletedNotification notification)
    {
        foreach (INotificationProvider provider in _notificationFactory.OnQueueItemDeletedEnabled())
        {
            try
            {
                await provider.OnQueueItemDeleted(notification);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "failed to send notification | provider {provider}", provider.Name);
            }
        }
    }
    
    public async Task Notify(DownloadCleanedNotification notification)
    {
        foreach (INotificationProvider provider in _notificationFactory.OnDownloadCleanedEnabled())
        {
            try
            {
                await provider.OnDownloadCleaned(notification);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "failed to send notification | provider {provider}", provider.Name);
            }
        }
    }
    
    public async Task Notify(CategoryChangedNotification notification)
    {
        foreach (INotificationProvider provider in _notificationFactory.OnCategoryChangedEnabled())
        {
            try
            {
                await provider.OnCategoryChanged(notification);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "failed to send notification | provider {provider}", provider.Name);
            }
        }
    }
}