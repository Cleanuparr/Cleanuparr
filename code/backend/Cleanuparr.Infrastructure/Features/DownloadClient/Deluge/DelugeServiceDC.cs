using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public partial class DelugeService
{
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var downloads = await _client.GetStatusForAllTorrents();
        if (downloads is null)
        {
            return [];
        }

        return downloads
            .Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => x.State?.Equals("seeding", StringComparison.InvariantCultureIgnoreCase) is true)
            .Select(ITorrentItemWrapper (x) => new DelugeItemWrapper(x))
            .ToList();
    }

    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<SeedingRule> seedingRules) =>
        downloads
            ?.Where(x => seedingRules.Any(cat => cat.Name.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, List<string> categories) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    /// <inheritdoc/>
    protected override async Task DeleteDownloadInternal(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        await DeleteDownload(torrent.Hash, deleteSourceFiles);
    }

    public override async Task CreateCategoryAsync(string name)
    {
        IReadOnlyList<string> existingLabels = await _client.GetLabels();

        if (existingLabels.Contains(name, StringComparer.InvariantCultureIgnoreCase))
        {
            return;
        }
        
        _logger.LogDebug("Creating category {name}", name);
        
        await _dryRunInterceptor.InterceptAsync(CreateLabel, name);
    }

    public override async Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

        foreach (DelugeItemWrapper torrent in downloads.Cast<DelugeItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Category))
            {
                continue;
            }
            
            ContextProvider.Set("downloadName", torrent.Name);
            ContextProvider.Set("hash", torrent.Hash);

            DelugeContents? contents;
            try
            {
                contents = await _client.GetTorrentFiles(torrent.Hash);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "failed to find torrent files for {name}", torrent.Name);
                continue;
            }

            bool hasHardlinks = false;
            bool hasErrors = false;

            ProcessFiles(contents?.Contents, (_, file) =>
            {
                string filePath = string.Join(Path.DirectorySeparatorChar, Path.Combine(torrent.Info.DownloadLocation, file.Path).Split(['\\', '/']));

                if (file.Priority <= 0)
                {
                    _logger.LogDebug("skip | file is not downloaded | {file}", filePath);
                    return;
                }

                long hardlinkCount = _hardLinkFileService
                    .GetHardLinkCount(filePath, downloadCleanerConfig.UnlinkedIgnoredRootDirs.Count > 0);

                if (hardlinkCount < 0)
                {
                    _logger.LogError("skip | file does not exist or insufficient permissions | {file}", filePath);
                    hasErrors = true;
                    return;
                }

                if (hardlinkCount > 0)
                {
                    hasHardlinks = true;
                }
            });

            if (hasErrors)
            {
                continue;
            }

            if (hasHardlinks)
            {
                _logger.LogDebug("skip | download has hardlinks | {name}", torrent.Name);
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(ChangeLabel, torrent.Hash, downloadCleanerConfig.UnlinkedTargetCategory);

            _logger.LogInformation("category changed for {name}", torrent.Name);

            await _eventPublisher.PublishCategoryChanged(torrent.Category, downloadCleanerConfig.UnlinkedTargetCategory);
            
            torrent.Category = downloadCleanerConfig.UnlinkedTargetCategory;
        }
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(string hash, bool deleteSourceFiles)
    {
        hash = hash.ToLowerInvariant();

        await _client.DeleteTorrents([hash], deleteSourceFiles);
    }

    protected async Task CreateLabel(string name)
    {
        await _client.CreateLabel(name);
    }
    
    protected virtual async Task ChangeLabel(string hash, string newLabel)
    {
        await _client.SetTorrentLabel(hash, newLabel);
    }
}