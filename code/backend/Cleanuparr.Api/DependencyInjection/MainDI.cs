using System.Text.Json.Serialization;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Consumers;
using Cleanuparr.Infrastructure.Features.Notifications.Consumers;
using Cleanuparr.Infrastructure.Features.Notifications.Models;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Data.Models.Arr;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;

namespace Cleanuparr.Api.DependencyInjection;

public static class MainDI
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddLogging(builder => builder.ClearProviders().AddConsole())
            .AddHttpClients(configuration)
            .AddSingleton<MemoryCache>()
            .AddSingleton<IMemoryCache>(serviceProvider => serviceProvider.GetRequiredService<MemoryCache>())
            .AddServices()
            .AddHealthServices()
            .AddQuartzServices(configuration)
            .AddNotifications(configuration)
            .AddMassTransit(config =>
            {
                config.AddConsumer<DownloadRemoverConsumer<SearchItem>>();
                config.AddConsumer<DownloadRemoverConsumer<SonarrSearchItem>>();
                
                config.AddConsumer<NotificationConsumer<FailedImportStrikeNotification>>();
                config.AddConsumer<NotificationConsumer<StalledStrikeNotification>>();
                config.AddConsumer<NotificationConsumer<SlowStrikeNotification>>();
                config.AddConsumer<NotificationConsumer<QueueItemDeletedNotification>>();
                config.AddConsumer<NotificationConsumer<DownloadCleanedNotification>>();
                config.AddConsumer<NotificationConsumer<CategoryChangedNotification>>();

                config.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureJsonSerializerOptions(options =>
                    {
                        options.Converters.Add(new JsonStringEnumConverter());
                        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;

                        return options;
                    });
                    
                    cfg.ReceiveEndpoint("download-remover-queue", e =>
                    {
                        e.ConfigureConsumer<DownloadRemoverConsumer<SearchItem>>(context);
                        e.ConfigureConsumer<DownloadRemoverConsumer<SonarrSearchItem>>(context);
                        e.ConcurrentMessageLimit = 1;
                        e.PrefetchCount = 1;
                    });
                    
                    cfg.ReceiveEndpoint("notification-queue", e =>
                    {
                        e.ConfigureConsumer<NotificationConsumer<FailedImportStrikeNotification>>(context);
                        e.ConfigureConsumer<NotificationConsumer<StalledStrikeNotification>>(context);
                        e.ConfigureConsumer<NotificationConsumer<SlowStrikeNotification>>(context);
                        e.ConfigureConsumer<NotificationConsumer<QueueItemDeletedNotification>>(context);
                        e.ConfigureConsumer<NotificationConsumer<DownloadCleanedNotification>>(context);
                        e.ConfigureConsumer<NotificationConsumer<CategoryChangedNotification>>(context);
                        e.ConcurrentMessageLimit = 1;
                        e.PrefetchCount = 1;
                    });
                });
            });
    
    private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        // Add the dynamic HTTP client system - this replaces all the previous static configurations
        services.AddDynamicHttpClients();
        
        // Add the dynamic HTTP client provider that uses the new system
        services.AddSingleton<IDynamicHttpClientProvider, DynamicHttpClientProvider>();
        
        return services;
    }

    /// <summary>
    /// Adds health check services to the service collection
    /// </summary>
    private static IServiceCollection AddHealthServices(this IServiceCollection services) =>
        services
            // Register the existing health check service for download clients
            .AddSingleton<IHealthCheckService, HealthCheckService>()
            
            // Register the background service for periodic health checks
            .AddHostedService<HealthCheckBackgroundService>()
            
            // Add ASP.NET Core health checks
            .AddHealthChecks()
                .AddCheck<ApplicationHealthCheck>("application", tags: ["liveness"])
                .AddCheck<DatabaseHealthCheck>("database", tags: ["readiness"])
                .AddCheck<FileSystemHealthCheck>("filesystem", tags: ["readiness"])
                .AddCheck<DownloadClientsHealthCheck>("download_clients", tags: ["readiness"])
            .Services;
}