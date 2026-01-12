using Cleanuparr.Persistence.Models.Configuration.Notification;

namespace Cleanuparr.Infrastructure.Features.Notifications.Discord;

public interface IDiscordProxy
{
    Task SendNotification(DiscordPayload payload, DiscordConfig config);
}
