using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DelugeService = Cleanuparr.Infrastructure.Features.DownloadClient.Deluge.DelugeService;
using QBitService = Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent.QBitService;
using TransmissionService = Cleanuparr.Infrastructure.Features.DownloadClient.Transmission.TransmissionService;
using UTorrentService = Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.UTorrentService;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Factory responsible for creating download client service instances
/// </summary>
public sealed class DownloadServiceFactory
{
    private readonly ILogger<DownloadServiceFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    public DownloadServiceFactory(
        ILogger<DownloadServiceFactory> logger,
        IServiceProvider serviceProvider
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Creates a download service using the specified client configuration
    /// </summary>
    /// <param name="downloadClientConfig">The client configuration to use</param>
    /// <returns>An implementation of IDownloadService or null if the client is not available</returns>
    public IDownloadService GetDownloadService(DownloadClientConfig downloadClientConfig)
    {
        if (!downloadClientConfig.Enabled)
        {
            _logger.LogWarning("Download client {clientId} is disabled, but a service was requested", downloadClientConfig.Id);
        }
        
        return downloadClientConfig.TypeName switch
        {
            DownloadClientTypeName.qBittorrent => CreateQBitService(downloadClientConfig),
            DownloadClientTypeName.Deluge => CreateDelugeService(downloadClientConfig),
            DownloadClientTypeName.Transmission => CreateTransmissionService(downloadClientConfig),
            DownloadClientTypeName.uTorrent => CreateUTorrentService(downloadClientConfig),
            _ => throw new NotSupportedException($"Download client type {downloadClientConfig.TypeName} is not supported")
        };
    }
    
    private QBitService CreateQBitService(DownloadClientConfig downloadClientConfig)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<QBitService>>();
        var cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        var filenameEvaluator = _serviceProvider.GetRequiredService<IFilenameEvaluator>();
        var striker = _serviceProvider.GetRequiredService<IStriker>();
        var dryRunInterceptor = _serviceProvider.GetRequiredService<IDryRunInterceptor>();
        var hardLinkFileService = _serviceProvider.GetRequiredService<IHardLinkFileService>();
        var httpClientProvider = _serviceProvider.GetRequiredService<IDynamicHttpClientProvider>();
        var eventPublisher = _serviceProvider.GetRequiredService<EventPublisher>();
        var blocklistProvider = _serviceProvider.GetRequiredService<BlocklistProvider>();

        var ruleEvaluator = _serviceProvider.GetRequiredService<IRuleEvaluator>();
        var ruleManager = _serviceProvider.GetRequiredService<IRuleManager>();

        // Create the QBitService instance
        QBitService service = new(
            logger, cache, filenameEvaluator, striker, dryRunInterceptor,
            hardLinkFileService, httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, ruleEvaluator, ruleManager
        );

        return service;
    }
    
    private DelugeService CreateDelugeService(DownloadClientConfig downloadClientConfig)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<DelugeService>>();
        var filenameEvaluator = _serviceProvider.GetRequiredService<IFilenameEvaluator>();
        var cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        var striker = _serviceProvider.GetRequiredService<IStriker>();
        var dryRunInterceptor = _serviceProvider.GetRequiredService<IDryRunInterceptor>();
        var hardLinkFileService = _serviceProvider.GetRequiredService<IHardLinkFileService>();
        var httpClientProvider = _serviceProvider.GetRequiredService<IDynamicHttpClientProvider>();
        var eventPublisher = _serviceProvider.GetRequiredService<EventPublisher>();
        var blocklistProvider = _serviceProvider.GetRequiredService<BlocklistProvider>();

        var ruleEvaluator = _serviceProvider.GetRequiredService<IRuleEvaluator>();
        var ruleManager = _serviceProvider.GetRequiredService<IRuleManager>();

        // Create the DelugeService instance
        DelugeService service = new(
            logger, cache, filenameEvaluator, striker, dryRunInterceptor,
            hardLinkFileService, httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, ruleEvaluator, ruleManager
        );

        return service;
    }

    private TransmissionService CreateTransmissionService(DownloadClientConfig downloadClientConfig)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<TransmissionService>>();
        var cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        var filenameEvaluator = _serviceProvider.GetRequiredService<IFilenameEvaluator>();
        var striker = _serviceProvider.GetRequiredService<IStriker>();
        var dryRunInterceptor = _serviceProvider.GetRequiredService<IDryRunInterceptor>();
        var hardLinkFileService = _serviceProvider.GetRequiredService<IHardLinkFileService>();
        var httpClientProvider = _serviceProvider.GetRequiredService<IDynamicHttpClientProvider>();
        var eventPublisher = _serviceProvider.GetRequiredService<EventPublisher>();
        var blocklistProvider = _serviceProvider.GetRequiredService<BlocklistProvider>();

        var ruleEvaluator = _serviceProvider.GetRequiredService<IRuleEvaluator>();
        var ruleManager = _serviceProvider.GetRequiredService<IRuleManager>();

        // Create the TransmissionService instance
        TransmissionService service = new(
            logger, cache, filenameEvaluator, striker, dryRunInterceptor,
            hardLinkFileService, httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, ruleEvaluator, ruleManager
        );

        return service;
    }

    private UTorrentService CreateUTorrentService(DownloadClientConfig downloadClientConfig)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<UTorrentService>>();
        var cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        var filenameEvaluator = _serviceProvider.GetRequiredService<IFilenameEvaluator>();
        var striker = _serviceProvider.GetRequiredService<IStriker>();
        var dryRunInterceptor = _serviceProvider.GetRequiredService<IDryRunInterceptor>();
        var hardLinkFileService = _serviceProvider.GetRequiredService<IHardLinkFileService>();
        var httpClientProvider = _serviceProvider.GetRequiredService<IDynamicHttpClientProvider>();
        var eventPublisher = _serviceProvider.GetRequiredService<EventPublisher>();
        var blocklistProvider = _serviceProvider.GetRequiredService<BlocklistProvider>();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();

        var ruleEvaluator = _serviceProvider.GetRequiredService<IRuleEvaluator>();
        var ruleManager = _serviceProvider.GetRequiredService<IRuleManager>();

        // Create the UTorrentService instance
        UTorrentService service = new(
            logger, cache, filenameEvaluator, striker, dryRunInterceptor,
            hardLinkFileService, httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, loggerFactory, ruleEvaluator, ruleManager
        );

        return service;
    }
}