using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public interface IAppriseCliProxy
{
    Task SendNotification(ApprisePayload payload, AppriseConfig config);
}
