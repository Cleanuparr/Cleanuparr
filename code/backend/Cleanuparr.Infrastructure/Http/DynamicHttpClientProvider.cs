using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Http;

/// <summary>
/// Provides dynamically configured HTTP clients for download services
/// </summary>
public class DynamicHttpClientProvider : IDynamicHttpClientProvider
{
    private readonly ILogger<DynamicHttpClientProvider> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDynamicHttpClientFactory _dynamicHttpClientFactory;

    public DynamicHttpClientProvider(
        ILogger<DynamicHttpClientProvider> logger,
        IServiceScopeFactory scopeFactory,
        IDynamicHttpClientFactory dynamicHttpClientFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _dynamicHttpClientFactory = dynamicHttpClientFactory;
    }

    /// <inheritdoc />
    public HttpClient CreateClient(DownloadClientConfig downloadClientConfig)
    {
        return CreateGenericClient(downloadClientConfig);
    }

    /// <summary>
    /// Gets the client name for a specific client configuration
    /// </summary>
    /// <param name="downloadClientConfig">The client configuration</param>
    /// <returns>The client name for use with IHttpClientFactory</returns>
    private string GetClientName(DownloadClientConfig downloadClientConfig)
    {
        return $"DownloadClient_{downloadClientConfig.Id}";
    }

    /// <summary>
    /// Creates a generic HTTP client with appropriate configuration using the dynamic system
    /// </summary>
    /// <param name="downloadClientConfig">The client configuration</param>
    /// <returns>A configured HttpClient instance</returns>
    private HttpClient CreateGenericClient(DownloadClientConfig downloadClientConfig)
    {
        using var scope = _scopeFactory.CreateScope();
        using var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        var httpConfig = dataContext.GeneralConfigs.First();
        var clientName = GetClientName(downloadClientConfig);
        
        // Determine the client type based on the download client type
        var clientType = downloadClientConfig.TypeName switch
        {
            DownloadClientTypeName.Deluge => HttpClientType.Deluge,
            _ => HttpClientType.WithRetry
        };

        // Create retry configuration
        var retryConfig = new RetryConfig
        {
            MaxRetries = httpConfig.HttpMaxRetries,
            ExcludeUnauthorized = true
        };

        // Register the client configuration dynamically
        _dynamicHttpClientFactory.RegisterDownloadClient(
            clientName,
            httpConfig.HttpTimeout,
            clientType,
            retryConfig,
            httpConfig.HttpCertificateValidation
        );

        // Create and configure the client
        var client = _dynamicHttpClientFactory.CreateClient(clientName);
        
        // Set base address if needed
        if (downloadClientConfig.Url != null)
        {
            client.BaseAddress = downloadClientConfig.Url;
        }
        
        _logger.LogTrace("Created HTTP client for download client {Name} (ID: {Id}) with type {Type}", 
            downloadClientConfig.Name, downloadClientConfig.Id, clientType);
        
        return client;
    }
}
