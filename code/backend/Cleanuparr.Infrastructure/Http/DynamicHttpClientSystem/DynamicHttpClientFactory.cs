using System.Collections.Concurrent;
using System.Net;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;

/// <summary>
/// Implementation of the dynamic HttpClient factory
/// </summary>
public class DynamicHttpClientFactory : IDynamicHttpClientFactory
{
    private const char VersionSeparator = '#';

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpClientConfigStore _configStore;
    private readonly IOptionsMonitorCache<HttpClientFactoryOptions> _optionsCache;
    private readonly ILogger<DynamicHttpClientFactory> _logger;
    private readonly ConcurrentDictionary<string, int> _generations = new();

    public DynamicHttpClientFactory(
        IHttpClientFactory httpClientFactory,
        IHttpClientConfigStore configStore,
        IOptionsMonitorCache<HttpClientFactoryOptions> optionsCache,
        ILogger<DynamicHttpClientFactory> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configStore = configStore;
        _optionsCache = optionsCache;
        _logger = logger;
    }

    /// <summary>
    /// Appends the generation so IHttpClientFactory builds a fresh handler. Generation 0 keeps the plain name.
    /// </summary>
    internal static string ComposeVersionedName(string clientName, int generation) =>
        generation == 0 ? clientName : $"{clientName}{VersionSeparator}{generation}";

    /// <summary>
    /// Recovers the configuration store key from a name built by <see cref="ComposeVersionedName"/>.
    /// </summary>
    internal static string StripVersion(string versionedName)
    {
        int index = versionedName.IndexOf(VersionSeparator);
        return index < 0 ? versionedName : versionedName[..index];
    }

    private int GetGeneration(string clientName) => _generations.GetOrAdd(clientName, 0);

    private void BumpGeneration(string clientName) => _generations.AddOrUpdate(clientName, 1, (_, generation) => generation + 1);

    public HttpClient CreateClient(string clientName)
    {
        if (!_configStore.TryGetConfiguration(clientName, out _))
        {
            throw new InvalidOperationException($"No configuration found for client '{clientName}'. Register configuration first.");
        }

        return _httpClientFactory.CreateClient(ComposeVersionedName(clientName, GetGeneration(clientName)));
    }

    public void RegisterConfiguration(string clientName, HttpClientConfig config)
    {
        // No bump: this runs on every download-service build, not only on changes.
        _configStore.AddConfiguration(clientName, config);
    }

    public void RegisterRetryClient(string clientName, int timeout, RetryConfig retryConfig, CertificateValidationType certificateType, bool sendUserAgent)
    {
        HttpClientConfig config = new()
        {
            Name = clientName,
            Timeout = timeout,
            Type = HttpClientType.WithRetry,
            RetryConfig = retryConfig,
            CertificateValidationType = certificateType,
            SendUserAgent = sendUserAgent
        };

        RegisterConfiguration(clientName, config);
    }

    public void RegisterDelugeClient(string clientName, int timeout, RetryConfig retryConfig, CertificateValidationType certificateType, bool sendUserAgent)
    {
        HttpClientConfig config = new()
        {
            Name = clientName,
            Timeout = timeout,
            Type = HttpClientType.Deluge,
            RetryConfig = retryConfig,
            AllowAutoRedirect = true,
            CertificateValidationType = certificateType,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            SendUserAgent = sendUserAgent
        };

        RegisterConfiguration(clientName, config);
    }

    public void RegisterDownloadClient(
        string clientName,
        int timeout,
        HttpClientType clientType,
        RetryConfig retryConfig,
        CertificateValidationType certificateType,
        bool sendUserAgent,
        HttpAuthType httpAuthType = HttpAuthType.Basic,
        NetworkCredential? httpAuthCredential = null,
        Uri? baseUri = null)
    {
        HttpClientConfig config = new()
        {
            Name = clientName,
            Timeout = timeout,
            Type = clientType,
            RetryConfig = retryConfig,
            CertificateValidationType = certificateType,
            SendUserAgent = sendUserAgent,
            HttpAuthType = httpAuthType,
            HttpAuthCredential = httpAuthCredential,
            BaseUri = baseUri,
        };

        // Configure Deluge-specific settings if needed
        if (clientType == HttpClientType.Deluge)
        {
            config.AllowAutoRedirect = true;
            config.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        }

        RegisterConfiguration(clientName, config);
    }

    public void RegisterPlainClient(string clientName, bool sendUserAgent)
    {
        HttpClientConfig config = new()
        {
            Name = clientName,
            Type = HttpClientType.Plain,
            SendUserAgent = sendUserAgent
        };

        RegisterConfiguration(clientName, config);
    }

    public void UnregisterConfiguration(string clientName)
    {
        _configStore.RemoveConfiguration(clientName);

        // The cached handler outlives the options, so only a new name drops stale credentials.
        _optionsCache.TryRemove(ComposeVersionedName(clientName, GetGeneration(clientName)));
        BumpGeneration(clientName);

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
            config.SendUserAgent = generalConfig.HttpSendUserAgent;

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
        foreach (string clientName in clientNames)
        {
            // Direct IHttpClientFactory consumers (ArrClient, notification proxies) request the bare name.
            _optionsCache.TryRemove(clientName);
            _optionsCache.TryRemove(ComposeVersionedName(clientName, GetGeneration(clientName)));
            BumpGeneration(clientName);
        }

        _logger.LogInformation("Updated and invalidated {Count} HTTP client configurations with new general settings: " +
                              "Timeout={Timeout}s, MaxRetries={MaxRetries}, CertificateValidation={CertValidation}",
                              updatedConfigurations.Count, 
                              generalConfig.HttpTimeout,
                              generalConfig.HttpMaxRetries,
                              generalConfig.HttpCertificateValidation);
    }
}