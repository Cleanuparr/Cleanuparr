using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.General;

namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Factory service to manage dynamic HttpClient creation
/// </summary>
public interface IDynamicHttpClientFactory
{
    /// <summary>
    /// Creates an HttpClient with the specified configuration and registers it for future use
    /// </summary>
    HttpClient CreateClient(string clientName, HttpClientConfig config);
    
    /// <summary>
    /// Creates an HttpClient using a previously registered configuration
    /// </summary>
    HttpClient CreateClient(string clientName);
    
    /// <summary>
    /// Registers a configuration for later use
    /// </summary>
    void RegisterConfiguration(string clientName, HttpClientConfig config);
    
    /// <summary>
    /// Registers a retry-enabled HttpClient configuration
    /// </summary>
    void RegisterRetryClient(string clientName, int timeout, RetryConfig retryConfig, CertificateValidationType certificateType);
    
    /// <summary>
    /// Registers a Deluge-specific HttpClient configuration
    /// </summary>
    void RegisterDelugeClient(string clientName, int timeout, RetryConfig retryConfig, CertificateValidationType certificateType);
    
    /// <summary>
    /// Registers a configuration for a download client
    /// </summary>
    void RegisterDownloadClient(string clientName, int timeout, HttpClientType clientType, RetryConfig retryConfig, CertificateValidationType certificateType);
    
    /// <summary>
    /// Unregisters a configuration
    /// </summary>
    void UnregisterConfiguration(string clientName);
    
    /// <summary>
    /// Updates all registered HTTP client configurations with new general config settings
    /// </summary>
    void UpdateAllClientsFromGeneralConfig(GeneralConfig generalConfig);
}