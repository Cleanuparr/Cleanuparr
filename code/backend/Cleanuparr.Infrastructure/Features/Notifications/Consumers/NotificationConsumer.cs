using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Infrastructure.Verticals.Notifications;
using MassTransit;
using Microsoft.Extensions.Logging;

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
                    await _notificationService.Notify(failedMessage);
                    break;
                case StalledStrikeNotification stalledMessage:
                    await _notificationService.Notify(stalledMessage);
                    break;
                case SlowStrikeNotification slowMessage:
                    await _notificationService.Notify(slowMessage);
                    break;
                case QueueItemDeletedNotification queueItemDeleteMessage:
                    await _notificationService.Notify(queueItemDeleteMessage);
                    break;
                case DownloadCleanedNotification downloadCleanedNotification:
                    await _notificationService.Notify(downloadCleanedNotification);
                    break;
                case CategoryChangedNotification categoryChangedNotification:
                    await _notificationService.Notify(categoryChangedNotification);
                    break;
                default:
                    throw new NotImplementedException();
            }
                
            // prevent spamming
            await Task.Delay(1000);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "error while processing notifications");
        }
    }
}