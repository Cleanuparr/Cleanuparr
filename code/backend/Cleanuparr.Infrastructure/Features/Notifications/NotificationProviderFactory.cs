using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Discord;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
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
            NotificationProviderType.Pushover => CreatePushoverProvider(config),
            NotificationProviderType.Telegram => CreateTelegramProvider(config),
            NotificationProviderType.Discord => CreateDiscordProvider(config),
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
        var apiProxy = _serviceProvider.GetRequiredService<IAppriseProxy>();
        var cliProxy = _serviceProvider.GetRequiredService<IAppriseCliProxy>();

        return new AppriseProvider(config.Name, config.Type, appriseConfig, apiProxy, cliProxy);
    }

    private INotificationProvider CreateNtfyProvider(NotificationProviderDto config)
    {
        var ntfyConfig = (NtfyConfig)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<INtfyProxy>();

        return new NtfyProvider(config.Name, config.Type, ntfyConfig, proxy);
    }

    private INotificationProvider CreatePushoverProvider(NotificationProviderDto config)
    {
        var pushoverConfig = (PushoverConfig)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<IPushoverProxy>();

        return new PushoverProvider(config.Name, config.Type, pushoverConfig, proxy);
    }

    private INotificationProvider CreateTelegramProvider(NotificationProviderDto config)
    {
        var telegramConfig = (TelegramConfig)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<ITelegramProxy>();

        return new TelegramProvider(config.Name, config.Type, telegramConfig, proxy);
    }

    private INotificationProvider CreateDiscordProvider(NotificationProviderDto config)
    {
        var discordConfig = (DiscordConfig)config.Configuration;
        var proxy = _serviceProvider.GetRequiredService<IDiscordProxy>();

        return new DiscordProvider(config.Name, config.Type, discordConfig, proxy);
    }
}
