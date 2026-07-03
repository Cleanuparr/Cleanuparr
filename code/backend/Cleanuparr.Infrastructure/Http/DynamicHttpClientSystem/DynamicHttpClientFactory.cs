using System.Net;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Implementation of the dynamic HttpClient factory
/// </summary>
public class DynamicHttpClientFactory : IDynamicHttpClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpClientConfigStore _configStore;
    private readonly IHttpClientOptionsInvalidator _optionsInvalidator;
    private readonly ILogger<DynamicHttpClientFactory> _logger;

    public DynamicHttpClientFactory(
        IHttpClientFactory httpClientFactory, 
        IHttpClientConfigStore configStore,
        IHttpClientOptionsInvalidator optionsInvalidator,
        ILogger<DynamicHttpClientFactory> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configStore = configStore;
        _optionsInvalidator = optionsInvalidator;
        _logger = logger;
    }

    public HttpClient CreateClient(string clientName, HttpClientConfig config)
    {
        _configStore.AddConfiguration(clientName, config);
        return _httpClientFactory.CreateClient(clientName);
    }

    public HttpClient CreateClient(string clientName)
    {
        if (!_configStore.TryGetConfiguration(clientName, out _))
        {
            throw new InvalidOperationException($"No configuration found for client '{clientName}'. Register configuration first.");
        }
        
        return _httpClientFactory.CreateClient(clientName);
    }

    public void RegisterConfiguration(string clientName, HttpClientConfig config)
    {
        _configStore.AddConfiguration(clientName, config);
    }

    public void RegisterRetryClient(string clientName, int timeout, RetryConfig retryConfig, CertificateValidationType certificateType)
    {
        var config = new HttpClientConfig
        {
            Name = clientName,
            Timeout = timeout,
            Type = HttpClientType.WithRetry,
            RetryConfig = retryConfig,
            CertificateValidationType = certificateType
        };
        
        RegisterConfiguration(clientName, config);
    }

    public void RegisterDelugeClient(string clientName, int timeout, RetryConfig retryConfig, CertificateValidationType certificateType)
    {
        var config = new HttpClientConfig
        {
            Name = clientName,
            Timeout = timeout,
            Type = HttpClientType.Deluge,
            RetryConfig = retryConfig,
            AllowAutoRedirect = true,
            CertificateValidationType = certificateType,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        
        RegisterConfiguration(clientName, config);
    }

    public void RegisterDownloadClient(string clientName, int timeout, HttpClientType clientType, RetryConfig retryConfig, CertificateValidationType certificateType)
    {
        var config = new HttpClientConfig
        {
            Name = clientName,
            Timeout = timeout,
            Type = clientType,
            RetryConfig = retryConfig,
            CertificateValidationType = certificateType
        };

        // Configure Deluge-specific settings if needed
        if (clientType == HttpClientType.Deluge)
        {
            config.AllowAutoRedirect = true;
            config.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        }
        
        RegisterConfiguration(clientName, config);
    }

    public void UnregisterConfiguration(string clientName)
    {
        _configStore.RemoveConfiguration(clientName);
        
        // Also invalidate the cached options for this client
        _optionsInvalidator.InvalidateClient(clientName);
        
        _logger.LogDebug("Unregistered and invalidated HTTP client configuration: {ClientName}", clientName);
    }

    public void UpdateAllClientsFromGeneralConfig(GeneralConfig generalConfig)
    {
        var allConfigurations = _configStore.GetAllConfigurations().ToList();
        
        if (!allConfigurations.Any())
        {
            _logger.LogDebug("No HTTP client configurations to update");
            return;
        }

        var updatedConfigurations = allConfigurations.Select(kvp =>
        {
            var config = kvp.Value;
            
            // Update timeout and certificate validation for all clients
            config.Timeout = generalConfig.HttpTimeout;
            config.CertificateValidationType = generalConfig.HttpCertificateValidation;
            
            // Update retry configuration if it exists
            if (config.RetryConfig != null)
            {
                config.RetryConfig.MaxRetries = generalConfig.HttpMaxRetries;
            }
            
            return new KeyValuePair<string, HttpClientConfig>(kvp.Key, config);
        }).ToList();

        // Apply all updates to our configuration store
        _configStore.UpdateConfigurations(updatedConfigurations);
        
        // CRITICAL: Invalidate IHttpClientFactory's cached configurations
        // This forces the factory to call our Configure() method again with updated settings
        var clientNames = updatedConfigurations.Select(kvp => kvp.Key).ToList();
        _optionsInvalidator.InvalidateClients(clientNames);
        
        _logger.LogInformation("Updated and invalidated {Count} HTTP client configurations with new general settings: " +
                              "Timeout={Timeout}s, MaxRetries={MaxRetries}, CertificateValidation={CertValidation}",
                              updatedConfigurations.Count, 
                              generalConfig.HttpTimeout,
                              generalConfig.HttpMaxRetries,
                              generalConfig.HttpCertificateValidation);
    }
}