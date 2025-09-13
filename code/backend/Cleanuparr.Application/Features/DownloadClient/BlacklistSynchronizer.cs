using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Application.Features.DownloadClient;

public sealed class BlacklistSynchronizer : IHandler
{
    private readonly ILogger<BlacklistSynchronizer> _logger;
    private readonly DataContext _dataContext;
    private readonly DownloadServiceFactory _downloadServiceFactory;
    
    public BlacklistSynchronizer(
        ILogger<BlacklistSynchronizer> logger,
        DataContext dataContext,
        DownloadServiceFactory downloadServiceFactory
    )
    {
        _logger = logger;
        _dataContext = dataContext;
        _downloadServiceFactory = downloadServiceFactory;
    }

    public async Task ExecuteAsync()
    {
        GeneralConfig generalConfig = await _dataContext.GeneralConfigs
            .AsNoTracking()
            .FirstAsync();
        
        if (!generalConfig.EnableBlacklistSync)
        {
            _logger.LogDebug("Blacklist sync is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(generalConfig.BlacklistPath))
        {
            _logger.LogWarning("Blacklist sync path is not configured");
            return;
        }

        List<DownloadClientConfig> qBittorrentClients = await _dataContext.DownloadClients
            .AsNoTracking()
            .Where(c => c.Enabled && c.TypeName == DownloadClientTypeName.qBittorrent)
            .ToListAsync();

        if (qBittorrentClients.Count == 0)
        {
            _logger.LogDebug("No enabled qBittorrent clients found for blacklist sync");
            return;
        }

        _logger.LogDebug("Starting blacklist synchronization for {Count} qBittorrent clients", qBittorrentClients.Count);

        foreach (var clientConfig in qBittorrentClients)
        {
            try
            {
                var downloadService = _downloadServiceFactory.GetDownloadService(clientConfig);
                if (downloadService is not QBitService qBitService)
                {
                    _logger.LogError("Expected QBitService but got {ServiceType} for client {ClientName}", downloadService.GetType().Name, clientConfig.Name);
                    continue;
                }

                try
                {
                    await qBitService.LoginAsync();
                    await qBitService.UpdateBlacklistAsync(generalConfig.BlacklistPath!);
                    _logger.LogDebug("Successfully updated blacklist for qBittorrent client {ClientName}", clientConfig.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update blacklist for qBittorrent client {ClientName}", clientConfig.Name);
                }
                finally
                {
                    qBitService.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create download service for client {ClientName}", clientConfig.Name);
            }
        }

        _logger.LogDebug("Blacklist synchronization completed");
    }
}
