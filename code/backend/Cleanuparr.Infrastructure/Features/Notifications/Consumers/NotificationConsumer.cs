using Cleanuparr.Infrastructure.Features.Notifications.Models;
using MassTransit;
using Microsoft.Extensions.Logging;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Notifications.Consumers;

public sealed class NotificationConsumer<T> : IConsumer<T> where T : Notification
{
    private readonly ILogger<NotificationConsumer<T>> _logger;
    private readonly NotificationService _notificationService;

    public NotificationConsumer(ILogger<NotificationConsumer<T>> logger, NotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<T> context)
    {
        try
        {
            switch (context.Message)
            {
                case FailedImportStrikeNotification failedMessage:
                    await _notificationService.SendNotificationAsync(
                        NotificationEventType.FailedImportStrike, 
                        ConvertToNotificationContext(failedMessage, NotificationEventType.FailedImportStrike));
                    break;
                case StalledStrikeNotification stalledMessage:
                    await _notificationService.SendNotificationAsync(
                        NotificationEventType.StalledStrike, 
                        ConvertToNotificationContext(stalledMessage, NotificationEventType.StalledStrike));
                    break;
                case SlowSpeedStrikeNotification slowMessage:
                    await _notificationService.SendNotificationAsync(
                        NotificationEventType.SlowSpeedStrike, 
                        ConvertToNotificationContext(slowMessage, NotificationEventType.SlowSpeedStrike));
                    break;
                case SlowTimeStrikeNotification slowTimeMessage:
                    await _notificationService.SendNotificationAsync(
                        NotificationEventType.SlowTimeStrike, 
                        ConvertToNotificationContext(slowTimeMessage, NotificationEventType.SlowTimeStrike));
                    break;
                case QueueItemDeletedNotification queueItemDeleteMessage:
                    await _notificationService.SendNotificationAsync(
                        NotificationEventType.QueueItemDeleted, 
                        ConvertToNotificationContext(queueItemDeleteMessage, NotificationEventType.QueueItemDeleted));
                    break;
                case DownloadCleanedNotification downloadCleanedNotification:
                    await _notificationService.SendNotificationAsync(
                        NotificationEventType.DownloadCleaned, 
                        ConvertToNotificationContext(downloadCleanedNotification, NotificationEventType.DownloadCleaned));
                    break;
                case CategoryChangedNotification categoryChangedNotification:
                    await _notificationService.SendNotificationAsync(
                        NotificationEventType.CategoryChanged, 
                        ConvertToNotificationContext(categoryChangedNotification, NotificationEventType.CategoryChanged));
                    break;
                default:
                    throw new NotImplementedException();
            }
                
            // prevent spamming
            await Task.Delay(1000);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while processing notifications");
        }
    }

    private static NotificationContext ConvertToNotificationContext(Notification notification, NotificationEventType eventType)
    {
        var severity = notification.Level switch
        {
            NotificationLevel.Important => EventSeverity.Important,
            NotificationLevel.Warning => EventSeverity.Warning,
            _ => EventSeverity.Information
        };

        var data = new Dictionary<string, string>();
        Uri? image = null;

        if (notification is ArrNotification arrNotification)
        {
            data.Add("Instance type", arrNotification.InstanceType.ToString());
            data.Add("Url", arrNotification.InstanceUrl.ToString());
            data.Add("Hash", arrNotification.Hash);

            image = arrNotification.Image;
        }

        foreach (var field in notification.Fields ?? [])
        {
            data[field.Key] = field.Value;
        }

        return new NotificationContext
        {
            EventType = eventType,
            Title = notification.Title,
            Description = notification.Description,
            Severity = severity,
            Data = data,
            Image = image
        };
    }
}