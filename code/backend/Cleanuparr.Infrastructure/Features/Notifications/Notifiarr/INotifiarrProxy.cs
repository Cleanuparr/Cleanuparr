using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public interface INotifiarrProxy
{
    Task SendNotification(NotifiarrPayload payload, NotifiarrConfig config);
}