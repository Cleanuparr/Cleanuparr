using Cleanuparr.Infrastructure.Features.Notifications;
using Cleanuparr.Infrastructure.Features.Notifications.Apprise;
using Cleanuparr.Infrastructure.Features.Notifications.Notifiarr;

namespace Cleanuparr.Api.DependencyInjection;

public static class NotificationsDI
{
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration) =>
        services
            // Legacy notification providers (will be deprecated)
            .AddTransient<INotifiarrProxy, NotifiarrProxy>()
            .AddTransient<IAppriseProxy, AppriseProxy>()
            .AddTransient<INotificationPublisher, NotificationPublisher>()
            
            // New notification system
            .AddScoped<INotificationConfigurationService, NotificationConfigurationService>()
            .AddScoped<INotificationProviderFactory, NotificationProviderFactory>()
            .AddScoped<NotificationProviderFactory>()
            .AddScoped<NotificationPublisherV2>()
            .AddScoped<NotificationServiceV2>()
            .AddScoped<NotificationTestService>();
}