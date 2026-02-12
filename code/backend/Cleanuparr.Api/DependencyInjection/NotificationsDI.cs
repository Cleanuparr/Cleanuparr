using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Discord;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;
using Cleanuparr.Infrastructure.Features.Notifications.Pushover;
using Cleanuparr.Infrastructure.Features.Notifications.Telegram;
using Cleanuparr.Infrastructure.Features.Notifications.Gotify;

namespace Cleanuparr.Api.DependencyInjection;

public static class NotificationsDI
{
    public static IServiceCollection AddNotifications(this IServiceCollection services) =>
        services
            .AddScoped<INotifiarrProxy, NotifiarrProxy>()
            .AddScoped<IAppriseProxy, AppriseProxy>()
            .AddScoped<IAppriseCliProxy, AppriseCliProxy>()
            .AddSingleton<IAppriseCliDetector, AppriseCliDetector>()
            .AddScoped<INtfyProxy, NtfyProxy>()
            .AddScoped<IPushoverProxy, PushoverProxy>()
            .AddScoped<ITelegramProxy, TelegramProxy>()
            .AddScoped<IDiscordProxy, DiscordProxy>()
            .AddScoped<IGotifyProxy, GotifyProxy>()
            .AddScoped<INotificationConfigurationService, NotificationConfigurationService>()
            .AddScoped<INotificationProviderFactory, NotificationProviderFactory>()
            .AddScoped<NotificationProviderFactory>()
            .AddScoped<INotificationPublisher, NotificationPublisher>()
            .AddScoped<NotificationService>();
}