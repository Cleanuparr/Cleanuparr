using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications.Consumers;

public sealed class NotificationConsumer : IConsumer<NotificationMessage>
{
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly INotificationConfigurationService _configurationService;
    private readonly INotificationProviderFactory _providerFactory;

    public NotificationConsumer(
        ILogger<NotificationConsumer> logger,
        INotificationConfigurationService configurationService,
        INotificationProviderFactory providerFactory
    )
    {
        _logger = logger;
        _configurationService = configurationService;
        _providerFactory = providerFactory;
    }

    public async Task Consume(ConsumeContext<NotificationMessage> context)
    {
        NotificationEventType eventType = context.Message.EventType;
        NotificationContext notificationContext = context.Message.Context;

        List<NotificationProviderDto> providers = await _configurationService.GetProvidersForEventAsync(eventType);

        if (!providers.Any())
        {
            _logger.LogDebug("No providers configured for event type {eventType}", eventType);
            return;
        }

        IEnumerable<Task> tasks = providers.Select(async providerConfig =>
        {
            try
            {
                INotificationProvider provider = _providerFactory.CreateProvider(providerConfig);
                await provider.SendNotificationAsync(notificationContext);
                _logger.LogDebug("Notification sent successfully via {providerName}", provider.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification via provider {providerName}", providerConfig.Name);
            }
        });

        await Task.WhenAll(tasks);
    }
}
