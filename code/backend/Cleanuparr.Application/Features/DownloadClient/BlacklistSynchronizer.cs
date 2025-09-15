using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Cleanuparr.Infrastructure.Helpers;
using System.Security.Cryptography;
using System.Text;

namespace Cleanuparr.Application.Features.DownloadClient;

public sealed class BlacklistSynchronizer : IHandler
{
    private readonly ILogger<BlacklistSynchronizer> _logger;
    private readonly DataContext _dataContext;
    private readonly DownloadServiceFactory _downloadServiceFactory;
    private readonly FileReader _fileReader;
    
    public BlacklistSynchronizer(
        ILogger<BlacklistSynchronizer> logger,
        DataContext dataContext,
        DownloadServiceFactory downloadServiceFactory,
        FileReader fileReader
    )
    {
        _logger = logger;
        _dataContext = dataContext;
        _downloadServiceFactory = downloadServiceFactory;
        _fileReader = fileReader;
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
        
        string[] patterns = await _fileReader.ReadContentAsync(generalConfig.BlacklistPath);
        string excludedFileNames = string.Join('\n', patterns.Where(p => !string.IsNullOrWhiteSpace(p)));

        string currentHash = ComputeHash(excludedFileNames);

        List<DownloadClientConfig> qBittorrentClients = await _dataContext.DownloadClients
            .AsNoTracking()
            .Where(c => c.Enabled && c.TypeName == DownloadClientTypeName.qBittorrent)
            .ToListAsync();

        if (qBittorrentClients.Count is 0)
        {
            _logger.LogDebug("No enabled qBittorrent clients found for blacklist sync");
            return;
        }

        _logger.LogDebug("Starting blacklist synchronization for {Count} qBittorrent clients | hash={hash}", qBittorrentClients.Count, currentHash);

        // Pull existing sync history for this hash
        var history = await _dataContext.BlacklistSyncHistory
            .AsNoTracking()
            .Where(s => s.Hash == currentHash)
            .ToListAsync();

        var alreadySynced = history
            .Select(s => s.DownloadClientId)
            .ToHashSet();

        // Only update clients not present in history for current hash
        foreach (var clientConfig in qBittorrentClients.Where(c => !alreadySynced.Contains(c.Id)))
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

        await RemoveOldDataAsync(currentHash);

        _logger.LogDebug("Blacklist synchronization completed");
    }

    private static string ComputeHash(string excludedFileNames)
    {
        using var sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(excludedFileNames);
        byte[] hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task RemoveOldDataAsync(string currentHash)
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
