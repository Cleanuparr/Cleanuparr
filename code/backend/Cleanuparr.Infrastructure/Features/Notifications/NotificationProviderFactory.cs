using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
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

    public INotificationProvider CreateProvider(NotificationProviderDto config)
    {
        return config.Type switch
        {
            NotificationProviderType.Notifiarr => CreateNotifiarrProvider(config),
            NotificationProviderType.Apprise => CreateAppriseProvider(config),
            NotificationProviderType.Ntfy => CreateNtfyProvider(config),
            _ => throw new NotSupportedException($"Provider type {config.Type} is not supported")
        };
    }

    private INotificationProvider CreateNotifiarrProvider(NotificationProviderDto config)
    {
        var notifiarrConfig = (NotifiarrConfig)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<INotifiarrProxy>();
        
        return new NotifiarrProvider(config.Name, config.Type, notifiarrConfig, proxy);
    }

    private INotificationProvider CreateAppriseProvider(NotificationProviderDto config)
    {
        var appriseConfig = (AppriseConfig)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<IAppriseProxy>();
        
        return new AppriseProvider(config.Name, config.Type, appriseConfig, proxy);
    }

    private INotificationProvider CreateNtfyProvider(NotificationProviderDto config)
    {
        var ntfyConfig = (NtfyConfig)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<INtfyProxy>();
        
        return new NtfyProvider(config.Name, config.Type, ntfyConfig, proxy);
    }
}
