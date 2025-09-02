using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Models;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public abstract class NotificationProviderBase<TConfig> : INotificationProvider
    where TConfig : class
{
    protected TConfig Config { get; }
    
    public string Name { get; }
    
    public NotificationProviderType Type { get; }

    protected NotificationProviderBase(string name, NotificationProviderType type, TConfig config)
    {
        Name = name;
        Type = type;
        Config = config;
    }

    public abstract Task SendNotificationAsync(NotificationContext context);
}
