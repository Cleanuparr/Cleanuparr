using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Service collection extensions for the dynamic HTTP client system
/// </summary>
public static class DynamicHttpClientServiceCollectionExtensions
{
    /// <summary>
    /// Adds the dynamic HTTP client system to the service collection
    /// This replaces the traditional AddHttpClients method
    /// </summary>
    public static IServiceCollection AddDynamicHttpClients(this IServiceCollection services)
    {
        // Register the dynamic system components
        services.AddSingleton<IHttpClientConfigStore, HttpClientConfigStore>();
        services.AddSingleton<IConfigureOptions<HttpClientFactoryOptions>, DynamicHttpClientConfiguration>();
        services.AddSingleton<IDynamicHttpClientFactory, DynamicHttpClientFactory>();

        // Add base HttpClient factory
        services.AddHttpClient();

        // Pre-register standard configurations using a hosted service
        services.AddHostedService<HttpClientConfigurationService>();

        return services;
    }
} 