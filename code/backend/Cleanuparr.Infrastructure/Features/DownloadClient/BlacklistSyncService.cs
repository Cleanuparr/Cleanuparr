using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Background service that synchronizes blacklist content to qBittorrent's excluded file names preference
/// Runs every hour to keep qBittorrent preferences updated with the latest blacklist content
/// </summary>
public class BlacklistSyncService : BackgroundService
{
    private readonly ILogger<BlacklistSyncService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1);

    public BlacklistSyncService(
        ILogger<BlacklistSyncService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Blacklist sync service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncBlacklistToQBittorrentAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during blacklist synchronization");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogDebug("Blacklist sync service stopped");
    }

    private async Task SyncBlacklistToQBittorrentAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        var downloadServiceFactory = scope.ServiceProvider.GetRequiredService<DownloadServiceFactory>();
        var generalConfig = await dataContext.GeneralConfigs.AsNoTracking().FirstAsync();
        
        if (!generalConfig.EnableBlacklistSync || string.IsNullOrEmpty(generalConfig.BlacklistPath))
        {
            _logger.LogDebug("Blacklist sync is disabled or path not configured");
            return;
        }

        _logger.LogDebug("Starting blacklist synchronization");

        try
        {
            // Get all enabled qBittorrent download clients
            var qBittorrentClients = await dataContext.DownloadClients
                .AsNoTracking()
                .Where(client => client.Enabled && client.TypeName == DownloadClientTypeName.qBittorrent)
                .ToListAsync();

            if (qBittorrentClients.Count == 0)
            {
                _logger.LogDebug("No enabled qBittorrent clients found");
                return;
            }

            _logger.LogTrace("Found {count} enabled qBittorrent clients for blacklist sync", qBittorrentClients.Count);

            // Process each qBittorrent client
            foreach (var clientConfig in qBittorrentClients)
            {
                try
                {
                    await SyncBlacklistToQBittorrentClient(downloadServiceFactory, clientConfig, generalConfig.BlacklistPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update blacklist for qBittorrent client {clientName}", clientConfig.Name);
                }
            }

            _logger.LogDebug("Blacklist synchronization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve qBittorrent clients for blacklist sync");
        }
    }

    private async Task SyncBlacklistToQBittorrentClient(
        DownloadServiceFactory downloadServiceFactory,
        DownloadClientConfig clientConfig,
        string blacklistPath
    )
    {
        _logger.LogDebug("Updating blacklist for qBittorrent client {clientName}", clientConfig.Name);

        var downloadService = downloadServiceFactory.GetDownloadService(clientConfig);
        
        if (downloadService is not QBitService qBitService)
        {
            _logger.LogError(
                "Expected QBitService but got {serviceType} for client {clientName}",
                downloadService.GetType().Name, clientConfig.Name
            );
            return;
        }

        try
        {
            await qBitService.LoginAsync();
            await qBitService.UpdateBlacklistAsync(blacklistPath);

            _logger.LogDebug("Successfully updated blacklist for qBittorrent client {clientName}", clientConfig.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to updated blacklist for qBittorrent client {clientName}", clientConfig.Name);
        }
        finally
        {
            qBitService.Dispose();
        }
    }
}