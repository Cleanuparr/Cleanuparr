using Cleanuparr.Domain.Entities.HealthCheck;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

public partial class SabnzbdService : DownloadService
{
    private readonly ISabnzbdClientWrapper _client;

    public SabnzbdService(
        ILogger<SabnzbdService> logger,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        IEventPublisher eventPublisher,
        IBlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        IQueueRuleEvaluator queueRuleEvaluator,
        ISeedingRuleEvaluator seedingRuleEvaluator,
        TimeProvider timeProvider
    ) : base(
        logger, filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, queueRuleEvaluator, seedingRuleEvaluator, timeProvider
    )
    {
        var sabnzbdClient = new SabnzbdClient(downloadClientConfig, _httpClient);
        _client = new SabnzbdClientWrapper(sabnzbdClient);
    }

    // Internal constructor for testing
    internal SabnzbdService(
        ILogger<SabnzbdService> logger,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        IEventPublisher eventPublisher,
        IBlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        IQueueRuleEvaluator queueRuleEvaluator,
        ISeedingRuleEvaluator seedingRuleEvaluator,
        TimeProvider timeProvider,
        ISabnzbdClientWrapper clientWrapper
    ) : base(
        logger, filenameEvaluator, striker, dryRunInterceptor, hardLinkFileService,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, queueRuleEvaluator, seedingRuleEvaluator, timeProvider
    )
    {
        _client = clientWrapper;
    }

    public override async Task LoginAsync()
    {
        try
        {
            await _client.GetVersionAsync();
            _logger.LogDebug("Successfully connected to SABnzbd client {ClientId}", _downloadClientConfig.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SABnzbd client {ClientId}", _downloadClientConfig.Id);
            throw;
        }
    }

    public override async Task<HealthCheckResult> HealthCheckAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _client.GetVersionAsync();
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

            _logger.LogWarning(ex, "Health check failed for SABnzbd client {ClientId}", _downloadClientConfig.Id);

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
}
