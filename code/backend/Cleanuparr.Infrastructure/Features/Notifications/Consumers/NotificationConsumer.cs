using Cleanuparr.Infrastructure.Features.Notifications.Models;
using MassTransit;

namespace Cleanuparr.Infrastructure.Features.Notifications.Consumers;

public sealed class NotificationConsumer : IConsumer<NotificationMessage>
{
    private readonly NotificationService _notificationService;

    public NotificationConsumer(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Consume(ConsumeContext<NotificationMessage> context) =>
        _notificationService.SendNotificationAsync(context.Message.EventType, context.Message.Context);
}
