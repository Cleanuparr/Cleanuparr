using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DelugeService = Cleanuparr.Infrastructure.Features.DownloadClient.Deluge.DelugeService;
using QBitService = Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent.QBitService;
using RTorrentService = Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent.RTorrentService;
using TransmissionService = Cleanuparr.Infrastructure.Features.DownloadClient.Transmission.TransmissionService;
using UTorrentService = Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.UTorrentService;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Factory responsible for creating download client service instances
/// </summary>
public sealed class DownloadServiceFactory : IDownloadServiceFactory
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
    /// <returns>An implementation of IDownloadService</returns>
    public IDownloadService GetDownloadService(DownloadClientConfig downloadClientConfig)
    {
        if (!downloadClientConfig.Enabled)
        {
            _logger.LogWarning("Download client {clientId} is disabled, but a service was requested", downloadClientConfig.Id);
        }

        return downloadClientConfig.TypeName switch
        {
            DownloadClientTypeName.qBittorrent => ActivatorUtilities.CreateInstance<QBitService>(_serviceProvider, downloadClientConfig),
            DownloadClientTypeName.Deluge => ActivatorUtilities.CreateInstance<DelugeService>(_serviceProvider, downloadClientConfig),
            DownloadClientTypeName.Transmission => ActivatorUtilities.CreateInstance<TransmissionService>(_serviceProvider, downloadClientConfig),
            DownloadClientTypeName.uTorrent => ActivatorUtilities.CreateInstance<UTorrentService>(_serviceProvider, downloadClientConfig),
            DownloadClientTypeName.rTorrent => ActivatorUtilities.CreateInstance<RTorrentService>(_serviceProvider, downloadClientConfig),
            _ => throw new NotSupportedException($"Download client type {downloadClientConfig.TypeName} is not supported")
        };
    }
}
