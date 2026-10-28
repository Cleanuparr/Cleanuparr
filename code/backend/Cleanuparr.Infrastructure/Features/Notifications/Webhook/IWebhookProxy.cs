using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Webhook;

public interface IWebhookProxy
{
    Task SendNotification(WebhookPayload payload, WebhookConfig config);
}
