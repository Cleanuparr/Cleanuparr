using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public interface IAppriseProxy
{
    Task SendNotification(ApprisePayload payload, AppriseConfig config);
}