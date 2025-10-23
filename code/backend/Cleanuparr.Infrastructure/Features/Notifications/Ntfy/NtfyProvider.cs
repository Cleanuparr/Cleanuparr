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
        string message = BuildMessage(context);

        return new NtfyPayload
        {
            Topic = topic.Trim(),
            Title = context.Title,
            Message = message,
            Priority = (int)Config.Priority,
            Tags = Config.Tags.ToArray()
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

    private string[] GetTopics()
    {
        return Config.Topics
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .ToArray();
    }
}
