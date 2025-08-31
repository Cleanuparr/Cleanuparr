using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

namespace Cleanuparr.Api.DependencyInjection;

public static class NotificationsDI
{
    public static IServiceCollection AddNotifications(this IServiceCollection services) =>
        services
            .AddScoped<INotifiarrProxy, NotifiarrProxy>()
            .AddScoped<IAppriseProxy, AppriseProxy>()
            .AddScoped<INotificationConfigurationService, NotificationConfigurationService>()
            .AddScoped<INotificationProviderFactory, NotificationProviderFactory>()
            .AddScoped<NotificationProviderFactory>()
            .AddScoped<INotificationPublisher, NotificationPublisher>()
            .AddScoped<NotificationService>();
}