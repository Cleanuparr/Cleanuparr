namespace Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

public class NotifiarrPayload
{
    public NotifiarrNotification Notification { get; set; } = new NotifiarrNotification();
    public NotifiarrDiscord Discord { get; set; }
}
