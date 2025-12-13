namespace Cleanuparr.Infrastructure.Features.Notifications.Pushover;

public interface IPushoverProxy
{
    Task SendNotification(PushoverPayload payload);
}
