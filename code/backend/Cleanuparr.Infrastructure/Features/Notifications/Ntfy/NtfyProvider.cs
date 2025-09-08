using System.Text;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Ntfy;

public sealed class NtfyProvider : NotificationProviderBase<NtfyConfig>
{
    private readonly INtfyProxy _proxy;

    public NtfyProvider(
        string name,
        NotificationProviderType type,
        NtfyConfig config,
        INtfyProxy proxy
    ) : base(name, type, config)
    {
        _proxy = proxy;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        var topics = GetTopics();
        var tasks = topics.Select(topic => SendToTopic(topic, context));
        await Task.WhenAll(tasks);
    }

    private async Task SendToTopic(string topic, NotificationContext context)
    {
        NtfyPayload payload = BuildPayload(topic, context);
        await _proxy.SendNotification(payload, Config);
    }

    private NtfyPayload BuildPayload(string topic, NotificationContext context)
    {
        int priority = MapSeverityToPriority(context.Severity);
        string message = BuildMessage(context);
        string[]? tags = GetTags(context);

        return new NtfyPayload
        {
            Topic = topic.Trim(),
            Title = context.Title,
            Message = message,
            Priority = priority,
            Tags = tags
        };
    }

    private string BuildMessage(NotificationContext context)
    {
        var message = new StringBuilder();
        message.AppendLine(context.Description);
        
        if (context.Data.Any())
        {
            message.AppendLine();
            foreach ((string key, string value) in context.Data)
            {
                message.AppendLine($"{key}: {value}");
            }
        }

        return message.ToString().Trim();
    }

    private int MapSeverityToPriority(EventSeverity severity)
    {
        return severity switch
        {
            EventSeverity.Information => (int)Config.Priority,
            EventSeverity.Warning => Math.Max((int)Config.Priority, (int)NtfyPriority.High),
            EventSeverity.Important => (int)NtfyPriority.Max,
            _ => (int)Config.Priority
        };
    }

    private string[] GetTopics()
    {
        return Config.Topics
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();
    }

    private string[]? GetTags(NotificationContext context)
    {
        var tags = new List<string>();
        
        // Add default tags from config if any
        if (!string.IsNullOrWhiteSpace(Config.Tags))
        {
            var configTags = Config.Tags
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t));
            tags.AddRange(configTags);
        }
        
        // Add severity-based tag
        tags.Add(context.Severity.ToString().ToLowerInvariant());
        
        // Add event type tag
        tags.Add(context.EventType.ToString().ToLowerInvariant());

        return tags.Any() ? tags.ToArray() : null;
    }
}
