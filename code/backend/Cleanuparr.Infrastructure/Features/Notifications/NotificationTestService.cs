using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public sealed class NotificationTestService
{
    private readonly ILogger<NotificationTestService> _logger;
    private readonly INotificationProviderFactory _providerFactory;

    public NotificationTestService(
        ILogger<NotificationTestService> logger,
        INotificationProviderFactory providerFactory)
    {
        _logger = logger;
        _providerFactory = providerFactory;
    }

    public async Task<bool> TestProviderAsync(NotificationProviderDto config)
    {
        try
        {
            var testContext = new NotificationContext
            {
                EventType = NotificationEventType.Test,
                Title = "Test Notification from Cleanuparr",
                Description = "This is a test notification to verify your configuration is working correctly.",
                Severity = EventSeverity.Information,
                Data = new Dictionary<string, object>
                {
                    ["testTime"] = DateTime.UtcNow,
                    ["providerName"] = config.Name,
                    ["providerType"] = config.Type.ToString(),
                    ["hash"] = "test-hash-12345",
                    ["instanceType"] = InstanceType.Sonarr,
                    ["instanceUrl"] = "http://test-instance.local"
                }
            };

            var provider = _providerFactory.CreateProvider(config);
            await provider.SendNotificationAsync(testContext);

            _logger.LogTrace("Test notification sent successfully via {providerName}", config.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test notification via {providerName}", config.Name);
            throw;
        }
    }
}
