using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <inheritdoc cref="ISeedingRulesCleanupService" />
public sealed class SeedingRulesCleanupService : ISeedingRulesCleanupService
{
    private readonly ILogger<SeedingRulesCleanupService> _logger;
    private readonly DataContext _dataContext;

    public SeedingRulesCleanupService(ILogger<SeedingRulesCleanupService> logger, DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task CleanAsync(IDownloadService downloadService, List<ITorrentItemWrapper> clientDownloads)
    {
        try
        {
            DownloadClientConfig config = downloadService.ClientConfig;
            List<ISeedingRule> seedingRules = config.TypeName switch
            {
                DownloadClientTypeName.qBittorrent => (await _dataContext.QBitSeedingRules
                    .Where(r => r.DownloadClientConfigId == config.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.Deluge => (await _dataContext.DelugeSeedingRules
                    .Where(r => r.DownloadClientConfigId == config.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.Transmission => (await _dataContext.TransmissionSeedingRules
                    .Where(r => r.DownloadClientConfigId == config.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.uTorrent => (await _dataContext.UTorrentSeedingRules
                    .Where(r => r.DownloadClientConfigId == config.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.rTorrent => (await _dataContext.RTorrentSeedingRules
                    .Where(r => r.DownloadClientConfigId == config.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                _ => []
            };

            if (seedingRules.Count is 0)
            {
                _logger.LogDebug("No seeding rules found for {clientName}", downloadService.ClientConfig.Name);
                return;
            }
            
            List<ITorrentItemWrapper>? downloadsToClean = downloadService
                .FilterDownloadsToBeCleanedAsync(clientDownloads, seedingRules);

            if (downloadsToClean?.Count is null or 0)
            {
                return;
            }

            _logger.LogInformation("Evaluating {count} downloads for cleanup", downloadsToClean.Count);

            await downloadService.CleanDownloadsAsync(downloadsToClean, seedingRules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean downloads for {clientName}", downloadService.ClientConfig.Name);
        }

        _logger.LogInformation("Finished cleanup evaluation");
    }
}
