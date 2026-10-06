using Cleanuparr.Infrastructure.Features.Messaging;
using Cleanuparr.Infrastructure.Features.Notifications.Models;

namespace Cleanuparr.Infrastructure.Features.Notifications.Consumers;

/// <summary>
/// Sends one queued notification to its configured providers.
/// </summary>
public sealed class NotificationConsumer : IMessageHandler<NotificationMessage>
{
    private readonly NotificationService _notificationService;

    public NotificationConsumer(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <inheritdoc />
    public Task HandleAsync(NotificationMessage message) =>
        _notificationService.SendNotificationAsync(message.EventType, message.Context);
}
