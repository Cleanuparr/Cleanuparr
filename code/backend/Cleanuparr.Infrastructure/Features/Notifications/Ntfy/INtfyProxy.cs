using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Ntfy;

public interface INtfyProxy
{
    Task SendNotification(NtfyPayload payload, NtfyConfig config);
}
