using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public sealed class NotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly INotificationConfigurationService _configurationService;
    private readonly INotificationProviderFactory _providerFactory;

    public NotificationService(
        ILogger<NotificationService> logger,
        INotificationConfigurationService configurationService,
        INotificationProviderFactory providerFactory)
    {
        _logger = logger;
        _configurationService = configurationService;
        _providerFactory = providerFactory;
    }

    public async Task SendNotificationAsync(NotificationEventType eventType, NotificationContext context)
    {
        try
        {
            var providers = await _configurationService.GetProvidersForEventAsync(eventType);

            if (!providers.Any())
            {
                _logger.LogDebug("No providers configured for event type {eventType}", eventType);
                return;
            }

            var tasks = providers.Select(async providerConfig =>
            {
                try
                {
                    var provider = _providerFactory.CreateProvider(providerConfig);
                    await provider.SendNotificationAsync(context);
                    _logger.LogDebug("Notification sent successfully via {providerName}", provider.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send notification via provider {providerName}", providerConfig.Name);
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogTrace("Notification sent to {count} providers for event {eventType}", providers.Count, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notifications for event type {eventType}", eventType);
        }
    }

    public async Task SendTestNotificationAsync(NotificationProviderDto providerConfig)
    {
        NotificationContext testContext = new()
        {
            EventType = NotificationEventType.Test,
            Title = "Test Notification from Cleanuparr",
            Description = "This is a test notification to verify your configuration is working correctly.",
            Severity = EventSeverity.Information,
            Data = new Dictionary<string, string>
            {
                ["Test time"] = DateTime.UtcNow.ToString("o"),
                ["Provider type"] = providerConfig.Type.ToString(),
            }
        };

        try
        {
            var provider = _providerFactory.CreateProvider(providerConfig);
            await provider.SendNotificationAsync(testContext);
            _logger.LogInformation("Test notification sent successfully via {providerName}", providerConfig.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test notification via {providerName}", providerConfig.Name);
            throw;
        }
    }
}
