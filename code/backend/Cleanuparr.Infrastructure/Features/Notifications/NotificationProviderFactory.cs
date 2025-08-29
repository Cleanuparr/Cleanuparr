using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Microsoft.Extensions.DependencyInjection;

namespace Cleanuparr.Infrastructure.Features.Notifications;

public sealed class NotificationProviderFactory : INotificationProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public INotificationProviderV2 CreateProvider(NotificationProviderDto config)
    {
        return config.Type switch
        {
            NotificationProviderType.Notifiarr => CreateNotifiarrProvider(config),
            NotificationProviderType.Apprise => CreateAppriseProvider(config),
            _ => throw new NotSupportedException($"Provider type {config.Type} is not supported")
        };
    }

    private INotificationProviderV2 CreateNotifiarrProvider(NotificationProviderDto config)
    {
        var notifiarrConfig = (NotifiarrConfiguration)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<INotifiarrProxy>();
        
        return new NotifiarrProviderV2(config.Name, config.Type, notifiarrConfig, proxy);
    }

    private INotificationProviderV2 CreateAppriseProvider(NotificationProviderDto config)
    {
        var appriseConfig = (AppriseConfiguration)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<IAppriseProxy>();
        
        return new AppriseProviderV2(config.Name, config.Type, appriseConfig, proxy);
    }
}
