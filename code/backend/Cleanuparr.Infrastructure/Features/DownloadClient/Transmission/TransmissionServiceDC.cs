using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Transmission.API.RPC.Arguments;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

public partial class TransmissionService
{
    public override async Task<List<ITorrentItemWrapper>> GetSeedingDownloads()
    {
        var result = await _client.TorrentGetAsync(Fields);
        return result?.Torrents
            ?.Where(x => !string.IsNullOrEmpty(x.HashString))
            .Where(x => x.Status is 5 or 6 || x is { IsFinished: true, Status: 0 })
            .Select(ITorrentItemWrapper (x) => new TransmissionItemWrapper(x))
            .ToList() ?? [];
    }

    /// <inheritdoc/>
    public override async Task<List<ITorrentItemWrapper>> GetAllTorrentsLite()
    {
        var result = await _client.TorrentGetAsync(Fields);
        return result?.Torrents
            ?.Where(x => !string.IsNullOrEmpty(x.HashString))
            .Select(ITorrentItemWrapper (x) => new TransmissionItemWrapper(x))
            .ToList() ?? [];
    }

    /// <inheritdoc/>
    public override List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules)
    {
        return downloads
            ?.Where(x => seedingRules.Any(rule => rule.Categories.Any(cat => cat.Equals(x.Category, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    public override List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig)
    {
        return downloads
            ?.Where(x => !string.IsNullOrEmpty(x.Hash))
            .Where(x => unlinkedConfig.Categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .Where(x =>
            {
                if (unlinkedConfig.UseTag)
                {
                    return !x.Tags.Any(tag =>
                        tag.Equals(unlinkedConfig.TargetCategory, StringComparison.InvariantCultureIgnoreCase));
                }

                return true;
            })
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles)
    {
        var transmissionTorrent = (TransmissionItemWrapper)torrent;
        await _client.TorrentRemoveAsync([transmissionTorrent.Info.Id], deleteSourceFiles);
    }

    public override async Task CreateCategoryAsync(string name)
    {
        await Task.CompletedTask;
    }

    public override async Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (TransmissionItemWrapper torrent in downloads.Cast<TransmissionItemWrapper>())
        {
            if (string.IsNullOrEmpty(torrent.Hash) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Info.DownloadDir))
            {
                continue;
            }

            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
            SetDownloadClientContext();

            if (torrent.Info.Files is null || torrent.Info.FileStats is null)
            {
                _logger.LogDebug("skip | download has no files | {name}", torrent.Name);
                continue;
            }

            bool hasHardlinks = false;
            bool hasErrors = false;

            for (int i = 0; i < torrent.Info.Files.Length; i++)
            {
                TransmissionTorrentFiles file = torrent.Info.Files[i];
                TransmissionTorrentFileStats stats = torrent.Info.FileStats[i];

                if (stats.Wanted is null or false || string.IsNullOrEmpty(file.Name))
                {
                    continue;
                }

                string filePath = PathHelper.NormalizeAndRemap(
                    Path.Combine(torrent.Info.DownloadDir, file.Name),
                    _downloadClientConfig.DownloadDirectorySource,
                    _downloadClientConfig.DownloadDirectoryTarget);

                long hardlinkCount = _hardLinkFileService.GetHardLinkCount(filePath, unlinkedConfig.IgnoredRootDirs.Count > 0);

                if (hardlinkCount < 0)
                {
                    _logger.LogError("skip | file does not exist or insufficient permissions | {file}", filePath);
                    hasErrors = true;
                    break;
                }

                if (hardlinkCount > 0)
                {
                    hasHardlinks = true;
                    break;
                }
            }

            if (hasErrors)
            {
                continue;
            }

            if (hasHardlinks)
            {
                _logger.LogDebug("skip | download has hardlinks | {name}", torrent.Name);
                continue;
            }

            string currentCategory = torrent.Category ?? string.Empty;

            if (unlinkedConfig.UseTag)
            {
                string[] newLabels = torrent.Tags
                    .Append(unlinkedConfig.TargetCategory)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                await _dryRunInterceptor.InterceptAsync(() => ChangeLabels(torrent.Info.Id, newLabels));

                _logger.LogInformation("label added for {name}", torrent.Name);

                await _eventPublisher.PublishCategoryChanged(currentCategory, unlinkedConfig.TargetCategory, isTag: true);

                continue;
            }

            string newLocation = torrent.Info.GetNewLocationByAppend(unlinkedConfig.TargetCategory);

            await _dryRunInterceptor.InterceptAsync(() => ChangeDownloadLocation(torrent.Info.Id, newLocation));

            _logger.LogInformation("category changed for {name}", torrent.Name);

            await _eventPublisher.PublishCategoryChanged(currentCategory, unlinkedConfig.TargetCategory);

            torrent.Category = unlinkedConfig.TargetCategory;
        }
    }

    /// <inheritdoc/>
    public override async Task ChangeTorrentCategoryAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag)
    {
        var transmissionTorrent = (TransmissionItemWrapper)torrent;

        ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
        ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
        SetDownloadClientContext();

        string currentCategory = torrent.Category ?? string.Empty;

        if (useTag)
        {
            string[] newLabels = torrent.Tags
                .Append(targetCategory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await _dryRunInterceptor.InterceptAsync(() => ChangeLabels(transmissionTorrent.Info.Id, newLabels));

            await _eventPublisher.PublishCategoryChanged(currentCategory, targetCategory, isTag: true);

            return;
        }

        string newLocation = transmissionTorrent.Info.GetNewLocationByAppend(targetCategory);

        await _dryRunInterceptor.InterceptAsync(() => ChangeDownloadLocation(transmissionTorrent.Info.Id, newLocation));

        await _eventPublisher.PublishCategoryChanged(currentCategory, targetCategory);

        torrent.Category = targetCategory;
    }

    protected virtual async Task ChangeDownloadLocation(long downloadId, string newLocation)
    {
        await _client.TorrentSetLocationAsync([downloadId], newLocation, true);
    }

    protected virtual async Task ChangeLabels(long downloadId, string[] labels)
    {
        await _client.TorrentSetAsync(new TorrentSettings
        {
            Ids = [downloadId],
            Labels = labels,
        });
    }
}
