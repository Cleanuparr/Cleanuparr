using System.Security.Cryptography;
using System.Text;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.BlacklistSync;

public sealed class BlacklistSynchronizer : IHandler
{
    private readonly ILogger<BlacklistSynchronizer> _logger;
    private readonly DataContext _dataContext;
    private readonly DownloadServiceFactory _downloadServiceFactory;
    private readonly FileReader _fileReader;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    
    public BlacklistSynchronizer(
        ILogger<BlacklistSynchronizer> logger,
        DataContext dataContext,
        DownloadServiceFactory downloadServiceFactory,
        FileReader fileReader,
        IDryRunInterceptor dryRunInterceptor
    )
    {
        _logger = logger;
        _dataContext = dataContext;
        _downloadServiceFactory = downloadServiceFactory;
        _fileReader = fileReader;
        _dryRunInterceptor = dryRunInterceptor;
    }

    public async Task ExecuteAsync()
    {
        BlacklistSyncConfig config = await _dataContext.BlacklistSyncConfigs
            .AsNoTracking()
            .FirstAsync();
        
        if (!config.Enabled)
        {
            _logger.LogDebug("Blacklist sync is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(config.BlacklistPath))
        {
            _logger.LogWarning("Blacklist sync path is not configured");
            return;
        }
        
        string[] patterns = await _fileReader.ReadContentAsync(config.BlacklistPath);
        string excludedFileNames = string.Join('\n', patterns.Where(p => !string.IsNullOrWhiteSpace(p)));

        string currentHash = ComputeHash(excludedFileNames);
        
    await _dryRunInterceptor.InterceptAsync(SyncBlacklist, currentHash, excludedFileNames);
        await _dryRunInterceptor.InterceptAsync(RemoveOldSyncDataAsync, currentHash);

        _logger.LogDebug("Blacklist synchronization completed");
    }

    private async Task SyncBlacklist(string currentHash, string excludedFileNames)
    {
        List<DownloadClientConfig> qBittorrentClients = await _dataContext.DownloadClients
            .AsNoTracking()
            .Where(c => c.Enabled && c.TypeName == DownloadClientTypeName.qBittorrent)
            .ToListAsync();
        
        if (qBittorrentClients.Count is 0)
        {
            _logger.LogDebug("No enabled qBittorrent clients found for blacklist sync");
            return;
        }

        _logger.LogDebug("Starting blacklist synchronization for {Count} qBittorrent clients", qBittorrentClients.Count);

        // Pull existing sync history for this hash
        var alreadySynced = await _dataContext.BlacklistSyncHistory
            .AsNoTracking()
            .Where(s => s.Hash == currentHash)
            .Select(x => x.DownloadClientId)
            .ToListAsync();

        // Only update clients not present in history for current hash
        foreach (var clientConfig in qBittorrentClients)
        {
            try
            {
                if (alreadySynced.Contains(clientConfig.Id))
                {
                    _logger.LogDebug("Client {ClientName} already synced for current blacklist, skipping", clientConfig.Name);
                    continue;
                }
                
                var downloadService = _downloadServiceFactory.GetDownloadService(clientConfig);
                if (downloadService is not QBitService qBitService)
                {
                    _logger.LogError("Expected QBitService but got {ServiceType} for client {ClientName}", downloadService.GetType().Name, clientConfig.Name);
                    continue;
                }

                try
                {
                    await qBitService.LoginAsync();
                    await qBitService.UpdateBlacklistAsync(excludedFileNames);
                    
                    _logger.LogDebug("Successfully updated blacklist for qBittorrent client {ClientName}", clientConfig.Name);

                    // Insert history row marking this client as synced for current hash
                    _dataContext.BlacklistSyncHistory.Add(new BlacklistSyncHistory
                    {
                        Hash = currentHash,
                        DownloadClientId = clientConfig.Id
                    });
                    await _dataContext.SaveChangesAsync();
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
    }

    private static string ComputeHash(string excludedFileNames)
    {
        using var sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(excludedFileNames);
        byte[] hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task RemoveOldSyncDataAsync(string currentHash)
    {
        try
        {
            await _dataContext.BlacklistSyncHistory
                .Where(s => s.Hash != currentHash)
                .ExecuteDeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old blacklist sync history");
        }
    }
}
