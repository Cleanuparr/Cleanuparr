using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

public partial class QBitService : DownloadService, IQBitService
{
    protected readonly IQBittorrentClientWrapper _client;

    public QBitService(
        ILogger<QBitService> logger,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        IEventPublisher eventPublisher,
        IBlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        IRuleEvaluator ruleEvaluator,
        IRuleManager ruleManager
    ) : base(
        logger, filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, ruleEvaluator, ruleManager
    )
    {
        var qBittorrentClient = new QBittorrentClient(_httpClient, downloadClientConfig.Url);
        _client = new QBittorrentClientWrapper(qBittorrentClient);
    }

    // Internal constructor for testing
    internal QBitService(
        ILogger<QBitService> logger,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        IEventPublisher eventPublisher,
        IBlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        IRuleEvaluator ruleEvaluator,
        IRuleManager ruleManager,
        IQBittorrentClientWrapper clientWrapper
    ) : base(
        logger, filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, ruleEvaluator, ruleManager
    )
    {
        _client = clientWrapper;
    }
    
    public override async Task LoginAsync()
    {
        if (string.IsNullOrEmpty(_downloadClientConfig.Username) && string.IsNullOrEmpty(_downloadClientConfig.Password))
        {
            _logger.LogDebug("No credentials configured for client {clientId}, skipping login", _downloadClientConfig.Id);
            return;
        }

        try
        {
            await _client.LoginAsync(_downloadClientConfig.Username, _downloadClientConfig.Password);
            _logger.LogDebug("Successfully logged in to qBittorrent client {clientId}", _downloadClientConfig.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to qBittorrent client {clientId}", _downloadClientConfig.Id);
            throw;
        }
    }

    public override async Task<HealthCheckResult> HealthCheckAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            bool hasCredentials = !string.IsNullOrEmpty(_downloadClientConfig.Username) || 
                                  !string.IsNullOrEmpty(_downloadClientConfig.Password);

            if (hasCredentials)
            {
                // If credentials are provided, we must be able to log in for the service to be healthy
                await _client.LoginAsync(_downloadClientConfig.Username, _downloadClientConfig.Password);
                _logger.LogDebug("Health check: Successfully logged in to qBittorrent client {clientId}", _downloadClientConfig.Id);
            }
            else
            {
                // If no credentials, test connectivity using version endpoint
                await _client.GetApiVersionAsync();
                _logger.LogDebug("Health check: Successfully connected to qBittorrent client {clientId}", _downloadClientConfig.Id);
            }

            stopwatch.Stop();

            return new HealthCheckResult
            {
                IsHealthy = true,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogWarning(ex, "Health check failed for qBittorrent client {clientId}", _downloadClientConfig.Id);
            
            return new HealthCheckResult
            {
                IsHealthy = false,
                ErrorMessage = $"Connection failed: {ex.Message}",
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Syncs blacklist patterns from configured file to qBittorrent excluded file names
    /// </summary>
    /// <param name="excludedFileNames">List of excluded file names for qBittorrent</param>
    public async Task UpdateBlacklistAsync(string excludedFileNames)
    {
        Preferences preferences = new()
        {
            AdditionalData = new Dictionary<string, JToken>
            {
                { "excluded_file_names", excludedFileNames }
            }
        };

        await _client.SetPreferencesAsync(preferences);
    }
    
    private async Task<IReadOnlyList<TorrentTracker>> GetTrackersAsync(string hash)
    {
        return (await _client.GetTorrentTrackersAsync(hash))
            .Where(x => x.Url.Contains("**"))
            .ToList();
    }
    
    public override void Dispose()
    {
        _client.Dispose();
    }
}