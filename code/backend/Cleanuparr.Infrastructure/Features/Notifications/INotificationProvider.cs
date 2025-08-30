using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public interface INotificationProvider
{
    string Name { get; }
    
    NotificationProviderType Type { get; }
    
    Task SendNotificationAsync(NotificationContext context);
}
