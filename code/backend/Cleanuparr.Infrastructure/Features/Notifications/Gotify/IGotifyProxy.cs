using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Gotify;

public interface IGotifyProxy
{
    Task SendNotification(GotifyPayload payload, GotifyConfig config);
}
