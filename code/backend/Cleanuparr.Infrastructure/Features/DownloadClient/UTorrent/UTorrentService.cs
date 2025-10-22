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

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// µTorrent download service implementation
/// Provides business logic layer on top of UTorrentClient
/// </summary>
public partial class UTorrentService : DownloadService, IUTorrentService
{
    private readonly UTorrentClient _client;

    public UTorrentService(
        ILogger<UTorrentService> logger,
        IMemoryCache cache,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        EventPublisher eventPublisher,
        BlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        ILoggerFactory loggerFactory,
        IRuleEvaluator ruleEvaluator,
        IRuleManager ruleManager
    ) : base(
        logger, cache,
        filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, ruleEvaluator, ruleManager
    )
    {
        // Create the new layered client with dependency injection
        var httpService = new UTorrentHttpService(_httpClient, downloadClientConfig, loggerFactory.CreateLogger<UTorrentHttpService>());
        var authenticator = new UTorrentAuthenticator(
            cache,
            httpService,
            downloadClientConfig,
            loggerFactory.CreateLogger<UTorrentAuthenticator>()
        );
        var responseParser = new UTorrentResponseParser(loggerFactory.CreateLogger<UTorrentResponseParser>());
        
        _client = new UTorrentClient(
            downloadClientConfig,
            authenticator,
            httpService,
            responseParser,
            loggerFactory.CreateLogger<UTorrentClient>()
        );
    }

    public override void Dispose()
    {
    }

    /// <summary>
    /// Authenticates with µTorrent Web UI
    /// </summary>
    public override async Task LoginAsync()
    {
        try
        {
            var loginSuccess = await _client.LoginAsync();
            
            if (!loginSuccess)
            {
                throw new InvalidOperationException("Failed to authenticate with µTorrent Web UI");
            }
            
            _logger.LogDebug("Successfully logged in to µTorrent client {clientId}", _downloadClientConfig.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to µTorrent client {clientId}", _downloadClientConfig.Id);
            throw;
        }
    }

    /// <summary>
    /// Performs health check for µTorrent service
    /// </summary>
    public override async Task<HealthCheckResult> HealthCheckAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Test authentication and basic connectivity
            await _client.LoginAsync();
            
            // Test API connectivity with a simple request
            var connectionOk = await _client.TestConnectionAsync();
            if (!connectionOk)
            {
                throw new InvalidOperationException("API connection test failed");
            }
            
            _logger.LogDebug("Health check: Successfully connected to µTorrent client {clientId}", _downloadClientConfig.Id);

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
            
            _logger.LogError(ex, "Health check failed for µTorrent client {clientId}", _downloadClientConfig.Id);

            return new HealthCheckResult
            {
                IsHealthy = false,
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message
            };
        }
    }
} 