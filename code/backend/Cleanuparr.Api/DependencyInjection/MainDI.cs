using System.Threading.Channels;
using Cleanuparr.Api.Json;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Consumers;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;
using Cleanuparr.Infrastructure.Features.Messaging;
using Cleanuparr.Infrastructure.Features.Notifications.Consumers;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Shared.Helpers;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;

namespace Cleanuparr.Api.DependencyInjection;

public static class MainDI
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddHttpClients(configuration)
            .AddSingleton<MemoryCache>()
            .AddSingleton<IMemoryCache>(serviceProvider => serviceProvider.GetRequiredService<MemoryCache>())
            .AddServices()
            .AddHealthServices()
            .AddQuartzServices(configuration)
            .AddNotifications()
            .AddMessageQueue<QueueItemRemoveRequest, DownloadRemoverConsumer>()
            .AddMassTransit(config =>
            {
                config.DisableUsageTelemetry();

                config.AddConsumer<NotificationConsumer>();

                config.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureJsonSerializerOptions(options =>
                    {
                        CleanuparrJsonConfiguration.ConfigureCore(options);

                        return options;
                    });

                    cfg.ReceiveEndpoint("notification-queue", e =>
                    {
                        e.ConfigureConsumer<NotificationConsumer>(context);
                        e.ConcurrentMessageLimit = 1;
                        e.PrefetchCount = 1;
                    });
                });
            });

    /// <summary>
    /// Registers an unbounded in-process queue for <typeparamref name="TMessage"/>,
    /// drained one message at a time by a <see cref="QueueWorker{TMessage}"/>.
    /// </summary>
    private static IServiceCollection AddMessageQueue<TMessage, THandler>(this IServiceCollection services)
        where THandler : class, IMessageHandler<TMessage>
    {
        Channel<TMessage> channel = Channel.CreateUnbounded<TMessage>(new UnboundedChannelOptions { SingleReader = true });

        return services
            .AddSingleton(channel)
            .AddSingleton(channel.Writer)
            .AddScoped<IMessageHandler<TMessage>, THandler>()
            .AddHostedService<QueueWorker<TMessage>>();
    }

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