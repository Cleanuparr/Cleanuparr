using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Transmission.API.RPC;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

public partial class TransmissionService : DownloadService, ITransmissionService
{
    private readonly Client _client;

    private static readonly string[] Fields =
    [
        TorrentFields.FILES,
        TorrentFields.FILE_STATS,
        TorrentFields.HASH_STRING,
        TorrentFields.ID,
        TorrentFields.ETA,
        TorrentFields.NAME,
        TorrentFields.STATUS,
        TorrentFields.IS_PRIVATE,
        TorrentFields.DOWNLOADED_EVER,
        TorrentFields.DOWNLOAD_DIR,
        TorrentFields.SECONDS_SEEDING,
        TorrentFields.UPLOAD_RATIO,
        TorrentFields.TRACKERS,
        TorrentFields.RATE_DOWNLOAD,
        TorrentFields.TOTAL_SIZE,
    ];

    public TransmissionService(
        ILogger<TransmissionService> logger,
        IMemoryCache cache,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        EventPublisher eventPublisher,
        BlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        IRuleEvaluator ruleEvaluator,
        IRuleManager ruleManager
    ) : base(
        logger, cache,
        filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, ruleEvaluator, ruleManager
    )
    {
        UriBuilder uriBuilder = new(_downloadClientConfig.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/rpc";
        _client = new Client(
            _httpClient,
            uriBuilder.Uri.ToString(),
            login: _downloadClientConfig.Username,
            password: _downloadClientConfig.Password
        );
    }

    public override async Task LoginAsync()
    {
        try 
        {
            await _client.GetSessionInformationAsync();
            _logger.LogDebug("Successfully logged in to Transmission client {clientId}", _downloadClientConfig.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to Transmission client {clientId}", _downloadClientConfig.Id);
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
                // If credentials are provided, we must be able to authenticate for the service to be healthy
                await _client.GetSessionInformationAsync();
                _logger.LogDebug("Health check: Successfully authenticated with Transmission client {clientId}", _downloadClientConfig.Id);
            }
            else
            {
                // If no credentials, test basic connectivity with a simple RPC call
                // This will likely fail with authentication error, but that tells us the service is running
                try
                {
                    await _client.GetSessionInformationAsync();
                    _logger.LogDebug("Health check: Successfully connected to Transmission client {clientId}", _downloadClientConfig.Id);
                }
                catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    // Authentication error means the service is running but requires credentials
                    _logger.LogDebug("Health check: Transmission client {clientId} is running but requires authentication", _downloadClientConfig.Id);
                }
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
            
            // Check if this is an authentication error when no credentials are provided
            bool isAuthError = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized");
            bool hasCredentials = !string.IsNullOrEmpty(_downloadClientConfig.Username) || 
                                  !string.IsNullOrEmpty(_downloadClientConfig.Password);
            
            if (isAuthError && !hasCredentials)
            {
                // Authentication error without credentials means service is running
                return new HealthCheckResult
                {
                    IsHealthy = true,
                    ResponseTime = stopwatch.Elapsed
                };
            }
            
            _logger.LogWarning(ex, "Health check failed for Transmission client {clientId}", _downloadClientConfig.Id);
            
            return new HealthCheckResult
            {
                IsHealthy = false,
                ErrorMessage = $"Connection failed: {ex.Message}",
                ResponseTime = stopwatch.Elapsed
            };
        }
    }
    
    public override void Dispose()
    {
    }

    private async Task<TorrentInfo?> GetTorrentAsync(string hash)
    {
        return (await _client.TorrentGetAsync(Fields, hash))
            ?.Torrents
            ?.FirstOrDefault();
    }
}