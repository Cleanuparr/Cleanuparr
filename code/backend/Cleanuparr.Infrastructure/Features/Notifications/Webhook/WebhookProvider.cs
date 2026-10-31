using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Webhook;

public sealed partial class WebhookProvider : NotificationProviderBase<WebhookConfig>
{
    private readonly IWebhookProxy _proxy;
    private readonly TimeProvider _timeProvider;

    public WebhookProvider(
        string name,
        NotificationProviderType type,
        WebhookConfig config,
        IWebhookProxy proxy,
        TimeProvider timeProvider)
        : base(name, type, config)
    {
        _proxy = proxy;
        _timeProvider = timeProvider;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        WebhookPayload payload = BuildPayload(context);
        await _proxy.SendNotification(payload, Config);
    }

    private WebhookPayload BuildPayload(NotificationContext context)
    {
        Dictionary<string, string> data = new();

        foreach ((string key, string value) in context.Data)
        {
            data[ToCamelCase(key)] = value;
        }

        return new WebhookPayload
        {
            EventType = context.EventType,
            Title = context.Title,
            Description = context.Description,
            Severity = context.Severity,
            Data = data,
            Image = context.Image?.ToString(),
            Timestamp = _timeProvider.GetUtcNow()
        };
    }

    /// <summary>
    /// Turns a display label into a camelCase key: "Removed from client?" becomes "removedFromClient".
    /// </summary>
    private static string ToCamelCase(string label)
    {
        string[] words = NonAlphanumeric().Split(label).Where(word => word.Length > 0).ToArray();

        return string.Concat(words.Select((word, index) => index == 0
            ? word.ToLowerInvariant()
            : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
