using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using System.Text;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public sealed class AppriseProviderV2 : NotificationProviderBaseV2<AppriseConfiguration>
{
    private readonly IAppriseProxy _proxy;

    public AppriseProviderV2(
        string name,
        NotificationProviderType type,
        AppriseConfiguration config,
        IAppriseProxy proxy)
        : base(name, type, config)
    {
        _proxy = proxy;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        var payload = BuildPayload(context);
        await _proxy.SendNotification(payload, Config);
    }

    private ApprisePayload BuildPayload(NotificationContext context)
    {
        var notificationType = context.Severity switch
        {
            EventSeverity.Warning => NotificationType.Warning,
            EventSeverity.Important => NotificationType.Failure,
            _ => NotificationType.Info
        };

        var body = BuildBody(context);

        return new ApprisePayload
        {
            Title = context.Title,
            Body = body,
            Type = notificationType.ToString().ToLowerInvariant(),
            Tags = Config.Tags,
        };
    }

    private string BuildBody(NotificationContext context)
    {
        var body = new StringBuilder();
        body.AppendLine(context.Description);
        body.AppendLine();

        if (context.Data.TryGetValue("instanceType", out var instanceType))
        {
            body.AppendLine($"Instance type: {instanceType}");
        }

        if (context.Data.TryGetValue("instanceUrl", out var instanceUrl))
        {
            body.AppendLine($"Url: {instanceUrl}");
        }

        if (context.Data.TryGetValue("hash", out var hash))
        {
            body.AppendLine($"Download hash: {hash}");
        }

        return body.ToString();
    }
}
