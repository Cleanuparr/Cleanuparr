using Cleanuparr.Infrastructure.Features.Notifications.Models;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public interface INotificationProviderFactory
{
    INotificationProvider CreateProvider(NotificationProviderDto config);
}
