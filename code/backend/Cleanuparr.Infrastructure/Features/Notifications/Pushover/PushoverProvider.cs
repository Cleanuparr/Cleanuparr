using System.Text;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Pushover;

public sealed class PushoverProvider : NotificationProviderBase<PushoverConfig>
{
    private readonly IPushoverProxy _proxy;

    public PushoverProvider(
        string name,
        NotificationProviderType type,
        PushoverConfig config,
        IPushoverProxy proxy
    ) : base(name, type, config)
    {
        _proxy = proxy;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        var payload = BuildPayload(context);
        await _proxy.SendNotification(payload);
    }

    private PushoverPayload BuildPayload(NotificationContext context)
    {
        string message = BuildMessage(context);

        // Truncate message to 1024 chars if needed
        if (message.Length > 1024)
        {
            message = message[..1021] + "...";
        }

        return new PushoverPayload
        {
            Token = Config.ApiToken,
            User = Config.UserKey,
            Message = message,
            Title = TruncateTitle(context.Title),
            Device = GetDevicesString(),
            Priority = (int)Config.Priority,
            Sound = Config.Sound,
            Retry = Config.Priority == PushoverPriority.Emergency ? Config.Retry : null,
            Expire = Config.Priority == PushoverPriority.Emergency ? Config.Expire : null,
            Tags = GetTagsString()
        };
    }

    private static string BuildMessage(NotificationContext context)
    {
        StringBuilder message = new();
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

    private static string? TruncateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return title.Length > 250 ? title[..247] + "..." : title;
    }

    private string? GetDevicesString()
    {
        string[] devices = Config.Devices
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .ToArray();

        return devices.Length > 0 ? string.Join(",", devices) : null;
    }

    private string? GetTagsString()
    {
        string[] tags = Config.Tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .ToArray();

        return tags.Length > 0 ? string.Join(",", tags) : null;
    }
}
