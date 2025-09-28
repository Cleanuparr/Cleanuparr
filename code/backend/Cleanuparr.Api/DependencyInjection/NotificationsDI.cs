using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;
using Cleanuparr.Infrastructure.Features.Notifications.Ntfy;

namespace Cleanuparr.Api.DependencyInjection;

public static class NotificationsDI
{
    public static IServiceCollection AddNotifications(this IServiceCollection services) =>
        services
            .AddScoped<INotifiarrProxy, NotifiarrProxy>()
            .AddScoped<IAppriseProxy, AppriseProxy>()
            .AddScoped<INtfyProxy, NtfyProxy>()
            .AddScoped<INotificationConfigurationService, NotificationConfigurationService>()
            .AddScoped<INotificationProviderFactory, NotificationProviderFactory>()
            .AddScoped<NotificationProviderFactory>()
            .AddScoped<INotificationPublisher, NotificationPublisher>()
            .AddScoped<NotificationService>();
}