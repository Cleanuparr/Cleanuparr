using Cleanuparr.Infrastructure.Features.Notifications.Models;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public interface INotificationProviderFactory
{
    INotificationProviderV2 CreateProvider(NotificationProviderDto config);
}
