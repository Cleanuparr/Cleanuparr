using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <inheritdoc cref="IDeadTorrentService" />
public sealed class DeadTorrentService : IDeadTorrentService
{
    private readonly ILogger<DeadTorrentService> _logger;
    private readonly DataContext _dataContext;
    private readonly IStriker _striker;

    public DeadTorrentService(
        ILogger<DeadTorrentService> logger,
        DataContext dataContext,
        IStriker striker)
    {
        _logger = logger;
        _dataContext = dataContext;
        _striker = striker;
    }

    public async Task ProcessAsync(IDownloadService downloadService, List<ITorrentItemWrapper> clientDownloads)
    {
        DeadTorrentConfig? config = await _dataContext.DeadTorrentConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DownloadClientConfigId == downloadService.ClientConfig.Id);

        if (config is not { Enabled: true })
        {
            return;
        }

        if (config.Categories.Count is 0)
        {
            _logger.LogWarning("Dead torrent config is enabled but no categories are configured for {name}", downloadService.ClientConfig.Name);
            return;
        }

        List<ITorrentItemWrapper> candidates = clientDownloads
            .Where(t => !string.IsNullOrEmpty(t.Hash))
            .Where(t => config.Categories.Any(cat => cat.Equals(t.Category, StringComparison.OrdinalIgnoreCase)))
            .Where(t => config.UseTag
                ? !t.Tags.Contains(config.TargetCategory, StringComparer.OrdinalIgnoreCase)
                : !config.TargetCategory.Equals(t.Category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count is 0)
        {
            return;
        }

        try
        {
            await downloadService.CreateCategoryAsync(config.TargetCategory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create category {category}", config.TargetCategory);
        }

        foreach (ITorrentItemWrapper torrent in candidates)
        {
            ContextProvider.SetDownloadClient(downloadService.ClientConfig);
            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);

            int? seederCount = torrent.SeederCount;

            if (!seederCount.HasValue)
            {
                _logger.LogDebug("skip dead torrent check | seeder count is unavailable | {name}", torrent.Name);
                continue;
            }

            if (seederCount.Value > 0)
            {
                await _striker.ResetStrikeAsync(torrent.Hash, torrent.Name, StrikeType.DeadTorrent);
                continue;
            }

            bool shouldMove = await _striker.StrikeAndCheckLimit(
                torrent.Hash,
                torrent.Name,
                (ushort)config.MaxStrikes,
                StrikeType.DeadTorrent);

            if (!shouldMove)
            {
                continue;
            }

            await downloadService.ChangeTorrentCategoryAsync(torrent, config.TargetCategory, config.UseTag);

            _logger.LogInformation(
                "dead torrent moved to {target} | tag: {useTag} | {name}",
                config.TargetCategory,
                config.UseTag,
                torrent.Name);
        }
    }
}
