using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using System.Text;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public sealed class AppriseProvider : NotificationProviderBase<AppriseConfig>
{
    private readonly IAppriseProxy _proxy;

    public AppriseProvider(
        string name,
        NotificationProviderType type,
        AppriseConfig config,
        IAppriseProxy proxy
    ) : base(name, type, config)
    {
        _proxy = proxy;
    }

    public override async Task SendNotificationAsync(NotificationContext context)
    {
        ApprisePayload payload = BuildPayload(context);
        await _proxy.SendNotification(payload, Config);
    }

    private ApprisePayload BuildPayload(NotificationContext context)
    {
        NotificationType notificationType = context.Severity switch
        {
            EventSeverity.Warning => NotificationType.Warning,
            EventSeverity.Important => NotificationType.Failure,
            _ => NotificationType.Info
        };

        string body = BuildBody(context);

        return new ApprisePayload
        {
            Title = context.Title,
            Body = body,
            Type = notificationType.ToString().ToLowerInvariant(),
            Tags = Config.Tags,
        };
    }

    private static string BuildBody(NotificationContext context)
    {
        var body = new StringBuilder();
        body.AppendLine(context.Description);
        body.AppendLine();
        
        foreach ((string key, string value) in context.Data)
        {
            body.AppendLine($"{key}: {value}");
        }

        return body.ToString();
    }
}
