using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Base for the torrent clients: everything that only makes sense for downloads that seed
/// (slow/stall rules, seeding-rule cleanup, hardlink-based unlinked moves, tags).
/// </summary>
public abstract class TorrentDownloadService : DownloadService, ISeedingCleanupCapable, IUnlinkedCapable
{
    protected readonly IStriker _striker;
    protected readonly IHardLinkFileService _hardLinkFileService;
    protected readonly IQueueRuleEvaluator _queueRuleEvaluator;
    protected readonly ISeedingRuleEvaluator _seedingRuleEvaluator;

    protected TorrentDownloadService(
        ILogger<DownloadService> logger,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        IEventPublisher eventPublisher,
        IBlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        IQueueRuleEvaluator queueRuleEvaluator,
        ISeedingRuleEvaluator seedingRuleEvaluator,
        TimeProvider timeProvider
    ) : base(
        logger, filenameEvaluator, dryRunInterceptor,
        httpClientProvider, eventPublisher, blocklistProvider, downloadClientConfig, timeProvider
    )
    {
        _striker = striker;
        _hardLinkFileService = hardLinkFileService;
        _queueRuleEvaluator = queueRuleEvaluator;
        _seedingRuleEvaluator = seedingRuleEvaluator;
    }

    protected virtual Task<bool> IsAltSpeedLimitActiveAsync()
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Runs the slow check, then the stall check; the first removal wins.
    /// </summary>
    protected async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient, bool ChangeCategory)> EvaluateDownloadRemoval(ITorrentItemWrapper wrapper)
    {
        (bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient, bool ChangeCategory) result = await CheckIfSlow(wrapper);

        if (result.ShouldRemove)
        {
            return result;
        }

        return await CheckIfStuck(wrapper);
    }

    /// <summary>
    /// Alt-speed-limit probe for the slow rules.
    /// Only qBittorrent and Transmission report that state.
    /// </summary>
    protected virtual Func<Task<bool>>? GetAltSpeedLimitProbe() => null;

    protected virtual async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient, bool ChangeCategory)> CheckIfSlow(ITorrentItemWrapper wrapper)
    {
        if (!wrapper.IsDownloading())
        {
            _logger.LogTrace("skip slow check | download is not in downloading state | {Name}", wrapper.Name);
            return (false, DeleteReason.None, false, false);
        }

        if (wrapper.DownloadSpeed <= 0)
        {
            _logger.LogTrace("skip slow check | download speed is 0 | {Name}", wrapper.Name);
            return (false, DeleteReason.None, false, false);
        }

        return await _queueRuleEvaluator.EvaluateSlowRulesAsync(wrapper, GetAltSpeedLimitProbe());
    }

    protected virtual async Task<(bool ShouldRemove, DeleteReason Reason, bool DeleteFromClient, bool ChangeCategory)> CheckIfStuck(ITorrentItemWrapper wrapper)
    {
        if (!wrapper.IsStalled())
        {
            _logger.LogTrace("skip stalled check | download is not in stalled state | {Name}", wrapper.Name);
            return (false, DeleteReason.None, false, false);
        }

        return await _queueRuleEvaluator.EvaluateStallRulesAsync(wrapper);
    }

    /// <inheritdoc cref="ISeedingCleanupCapable.GetSeedingDownloads"/>
    public abstract Task<List<ITorrentItemWrapper>> GetSeedingDownloads();

    /// <inheritdoc cref="ISeedingCleanupCapable.FilterDownloadsToBeCleanedAsync"/>
    public virtual List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules) =>
        downloads
            ?.Where(x => seedingRules.Any(rule => rule.Categories.Any(cat => cat.Equals(x.Category, StringComparison.OrdinalIgnoreCase))))
            .ToList();

    /// <inheritdoc cref="IUnlinkedCapable.FilterDownloadsToChangeCategoryAsync"/>
    public virtual List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig) =>
        downloads
            ?.Where(x => !string.IsNullOrEmpty(x.DownloadId))
            .Where(x => unlinkedConfig.Categories.Any(cat => cat.Equals(x.Category, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

    /// <inheritdoc cref="ISeedingCleanupCapable.CleanDownloadsAsync"/>
    public virtual async Task CleanDownloadsAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (ITorrentItemWrapper torrent in downloads)
        {
            if (string.IsNullOrEmpty(torrent.DownloadId))
            {
                continue;
            }

            ISeedingRule? seedingRule = _seedingRuleEvaluator.GetMatchingRule(torrent, seedingRules);

            if (seedingRule is null)
            {
                _logger.LogTrace("No seeding rules matched | {Name}", torrent.Name);
                continue;
            }

            _logger.LogTrace("Seeding rule matched | {SeedingRule} | {Name}", seedingRule.Name, torrent.Name);

            if (seedingRule.Action is SeedingRuleAction.Unknown)
            {
                _logger.LogWarning(
                    "Skipping seeding rule with an action this version does not know | {SeedingRule} | {Name}",
                    seedingRule.Name,
                    torrent.Name
                );
                continue;
            }

            if (seedingRule.Action is SeedingRuleAction.Stop && torrent.IsStopped)
            {
                continue;
            }

            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.DownloadId);
            SetDownloadClientContext();

            TimeSpan seedingTime = TimeSpan.FromSeconds(torrent.SeedingTimeSeconds);
            SeedingCheckResult result = ShouldCleanDownload(torrent.Ratio, seedingTime, torrent.SeederCount, torrent.LastActivityTime, seedingRule);

            if (!result.ShouldClean)
            {
                continue;
            }

            bool stopping = seedingRule.Action is SeedingRuleAction.Stop;

            try
            {
                await _dryRunInterceptor.InterceptAsync(() => stopping
                    ? StopDownload(torrent)
                    : DeleteDownload(torrent, seedingRule.DeleteSourceFiles));
            }
            catch (Exception exception)
            {
                // The download stays in the client. The run continues with the next download.
                _logger.LogError(exception, "failed to clean download | {Name}", torrent.Name);
                continue;
            }

            string reason = result.Reason is CleanReason.MaxRatioReached
                ? "MAX_RATIO & MIN_SEED_TIME"
                : "MAX_SEED_TIME";

            if (stopping)
            {
                _logger.LogInformation("download stopped | {Reason} reached | {Name}", reason, torrent.Name);

                await _eventPublisher.PublishDownloadStopped(torrent.Ratio, seedingTime, torrent.Category ?? string.Empty, result.Reason);
                continue;
            }

            _logger.LogInformation(
                "download cleaned | {Reason} reached | delete files: {DeleteFiles} | {Name}",
                reason,
                seedingRule.DeleteSourceFiles,
                torrent.Name
            );

            await _eventPublisher.PublishDownloadCleaned(torrent.Ratio, seedingTime, torrent.Category ?? string.Empty, result.Reason);
        }
    }

    /// <inheritdoc cref="IUnlinkedCapable.ChangeCategoryForNoHardLinksAsync"/>
    public async Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (ITorrentItemWrapper torrent in downloads)
        {
            if (string.IsNullOrEmpty(torrent.DownloadId) || string.IsNullOrEmpty(torrent.Name) || string.IsNullOrEmpty(torrent.Category))
            {
                continue;
            }

            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.DownloadId);
            SetDownloadClientContext();

            IEnumerable<(string FilePath, HardLinkScanAction Action)>? files = await GetHardLinkScanItemsAsync(torrent);

            if (files is null)
            {
                continue;
            }

            (bool hasHardlinks, bool hasErrors) = ScanForHardLinks(files, unlinkedConfig.IgnoredRootDirs.Count > 0);

            if (hasErrors)
            {
                continue;
            }

            if (hasHardlinks)
            {
                _logger.LogDebug("skip | download has hardlinks | {Name}", torrent.Name);
                continue;
            }

            await ChangeTorrentCategoryAsync(torrent, unlinkedConfig.TargetCategory, unlinkedConfig.UseTag);

            _logger.LogInformation(unlinkedConfig.UseTag && SupportsTags ? "tag added for {Name}" : "category changed for {Name}", torrent.Name);
        }
    }

    /// <summary>
    /// Maps a torrent's files to (path, <see cref="HardLinkScanAction"/>) pairs.
    /// Returns null to skip the torrent, such as when its files can't be read.
    /// </summary>
    protected abstract Task<IEnumerable<(string FilePath, HardLinkScanAction Action)>?> GetHardLinkScanItemsAsync(ITorrentItemWrapper torrent);

    /// <summary>
    /// Stops at the first hardlink or unreadable file.
    /// Each client maps its files to (path, <see cref="HardLinkScanAction"/>) pairs.
    /// </summary>
    protected (bool HasHardlinks, bool HasErrors) ScanForHardLinks(
        IEnumerable<(string FilePath, HardLinkScanAction Action)> files,
        bool ignoreRootDirs)
    {
        foreach ((string filePath, HardLinkScanAction action) in files)
        {
            if (action is HardLinkScanAction.TreatAsLinked)
            {
                return (true, false);
            }

            if (action is HardLinkScanAction.SkipUnwanted)
            {
                _logger.LogDebug("skip | file is not downloaded | {File}", filePath);
                continue;
            }

            long hardlinkCount = _hardLinkFileService.GetHardLinkCount(filePath, ignoreRootDirs);

            if (hardlinkCount < 0)
            {
                _logger.LogError("skip | file does not exist or insufficient permissions | {File}", filePath);
                return (false, true);
            }

            if (hardlinkCount > 0)
            {
                return (true, false);
            }
        }

        return (false, false);
    }

    /// <inheritdoc cref="IUnlinkedCapable.ChangeTorrentCategoryAsync"/>
    public async Task ChangeTorrentCategoryAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag)
    {
        ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
        ContextProvider.Set(ContextProvider.Keys.Hash, torrent.DownloadId);
        SetDownloadClientContext();

        string currentCategory = torrent.Category ?? string.Empty;
        useTag = useTag && SupportsTags;

        await _dryRunInterceptor.InterceptAsync(() => ChangeCategoryInClientAsync(torrent, targetCategory, useTag));

        await _eventPublisher.PublishCategoryChanged(currentCategory, targetCategory, useTag);

        if (!useTag)
        {
            torrent.Category = targetCategory;
        }
    }

    /// <summary>
    /// Clients without tags fall back to a category change when asked for a tag.
    /// </summary>
    protected virtual bool SupportsTags => false;

    /// <summary>
    /// Applies the category, or the tag when <paramref name="useTag"/> is set, in the client.
    /// </summary>
    protected abstract Task ChangeCategoryInClientAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag);

    /// <inheritdoc cref="IUnlinkedCapable.CreateCategoryAsync"/>
    public abstract Task CreateCategoryAsync(string name);

    /// <inheritdoc cref="ISeedingCleanupCapable.StopDownload"/>
    public abstract Task StopDownload(IDownloadItem item);

    private SeedingCheckResult ShouldCleanDownload(double ratio, TimeSpan seedingTime, int? seederCount, DateTime? lastActivity, ISeedingRule seedingRule)
    {
        if (BelowMinimumSeeders(seederCount, seedingRule))
        {
            return new();
        }

        if (StillActiveWithinWindow(lastActivity, seedingRule))
        {
            return new();
        }

        // check ratio
        if (DownloadReachedRatio(ratio, seedingTime, seedingRule))
        {
            return new()
            {
                ShouldClean = true,
                Reason = CleanReason.MaxRatioReached
            };
        }

        // check max seed time
        if (DownloadReachedMaxSeedTime(seedingTime, seedingRule))
        {
            return new()
            {
                ShouldClean = true,
                Reason = CleanReason.MaxSeedTimeReached
            };
        }

        return new();
    }

    private bool DownloadReachedRatio(double ratio, TimeSpan seedingTime, ISeedingRule seedingRule)
    {
        if (seedingRule.MaxRatio < 0)
        {
            return false;
        }

        string downloadName = ContextProvider.Get<string>(ContextProvider.Keys.ItemName);
        TimeSpan minSeedingTime = TimeSpan.FromHours(seedingRule.MinSeedTime);

        if (seedingRule.MinSeedTime > 0 && seedingTime < minSeedingTime)
        {
            _logger.LogDebug("skip | download has not reached MIN_SEED_TIME | {name}", downloadName);
            return false;
        }

        if (ratio < seedingRule.MaxRatio)
        {
            _logger.LogDebug("skip | download has not reached MAX_RATIO | {name}", downloadName);
            return false;
        }

        // max ratio is 0 or reached
        return true;
    }

    private bool BelowMinimumSeeders(int? seederCount, ISeedingRule seedingRule)
    {
        if (seedingRule is not ISeedersFilterable { MinSeeders: > 0 } seedersFilterable)
        {
            return false;
        }

        string downloadName = ContextProvider.Get<string>(ContextProvider.Keys.ItemName);

        if (!seederCount.HasValue)
        {
            _logger.LogDebug("skip | download seeder count is unavailable | {name}", downloadName);
            return true;
        }

        if (seederCount.Value >= seedersFilterable.MinSeeders)
        {
            return false;
        }

        _logger.LogDebug(
            "skip | download has fewer seeders than minimum | {seeders}/{minSeeders} | {name}",
            seederCount.Value,
            seedersFilterable.MinSeeders,
            downloadName);
        return true;

    }

    private bool StillActiveWithinWindow(DateTime? lastActivity, ISeedingRule seedingRule)
    {
        if (seedingRule is not IInactivityFilterable { MaxInactiveDays: >= 0 } inactivityRule)
        {
            return false;
        }

        string downloadName = ContextProvider.Get<string>(ContextProvider.Keys.ItemName);

        if (lastActivity is null || lastActivity <= DateTime.UnixEpoch)
        {
            _logger.LogDebug("skip | download last activity is unavailable | {name}", downloadName);
            return true;
        }

        double inactiveDays = (_timeProvider.GetUtcNow().UtcDateTime - lastActivity.Value).TotalDays;

        if (inactiveDays < inactivityRule.MaxInactiveDays)
        {
            _logger.LogDebug("skip | download still active within MAX_INACTIVE_DAYS | {name}", downloadName);
            return true;
        }

        return false;
    }

    private bool DownloadReachedMaxSeedTime(TimeSpan seedingTime, ISeedingRule seedingRule)
    {
        if (seedingRule.MaxSeedTime < 0)
        {
            return false;
        }

        string downloadName = ContextProvider.Get<string>(ContextProvider.Keys.ItemName);
        TimeSpan maxSeedingTime = TimeSpan.FromHours(seedingRule.MaxSeedTime);

        if (seedingRule.MaxSeedTime > 0 && seedingTime < maxSeedingTime)
        {
            _logger.LogDebug("skip | download has not reached MAX_SEED_TIME | {name}", downloadName);
            return false;
        }

        // max seed time is 0 or reached
        return true;
    }
}
