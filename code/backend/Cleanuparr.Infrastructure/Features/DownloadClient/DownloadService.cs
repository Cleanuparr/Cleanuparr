using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.HealthCheck;
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
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

public abstract class DownloadService : IDownloadService
{
    protected readonly ILogger<DownloadService> _logger;
    protected readonly IFilenameEvaluator _filenameEvaluator;
    protected readonly IStriker _striker;
    protected readonly IDryRunInterceptor _dryRunInterceptor;
    protected readonly IHardLinkFileService _hardLinkFileService;
    protected readonly IEventPublisher _eventPublisher;
    protected readonly IBlocklistProvider _blocklistProvider;
    protected readonly HttpClient _httpClient;
    protected readonly DownloadClientConfig _downloadClientConfig;
    protected readonly IQueueRuleEvaluator _queueRuleEvaluator;
    private readonly ISeedingRuleEvaluator _seedingRuleEvaluator;
    protected readonly TimeProvider _timeProvider;

    protected DownloadService(
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
    )
    {
        _logger = logger;
        _filenameEvaluator = filenameEvaluator;
        _striker = striker;
        _dryRunInterceptor = dryRunInterceptor;
        _hardLinkFileService = hardLinkFileService;
        _eventPublisher = eventPublisher;
        _blocklistProvider = blocklistProvider;
        _downloadClientConfig = downloadClientConfig;
        _httpClient = httpClientProvider.CreateClient(downloadClientConfig);
        _queueRuleEvaluator = queueRuleEvaluator;
        _seedingRuleEvaluator = seedingRuleEvaluator;
        _timeProvider = timeProvider;
    }
    
    public DownloadClientConfig ClientConfig => _downloadClientConfig;

    protected void SetDownloadClientContext()
    {
        ContextProvider.SetDownloadClient(_downloadClientConfig);
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

    public abstract void Dispose();

    public abstract Task LoginAsync();

    public abstract Task<HealthCheckResult> HealthCheckAsync();

    public abstract Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash, IReadOnlyList<string> ignoredDownloads);

    /// <inheritdoc/>
    public abstract Task<List<ITorrentItemWrapper>> GetSeedingDownloads();

    /// <inheritdoc/>
    public abstract Task<List<ITorrentItemWrapper>> GetAllTorrentsLite();

    /// <inheritdoc/>
    public abstract Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<ITorrentItemWrapper> torrents);

    /// <summary>
    /// Rejects a torrent list that lost any row the client reported.
    /// </summary>
    /// <param name="reportedCount">Rows the client sent.</param>
    /// <param name="usableCount">Rows that survived parsing and filtering.</param>
    /// <exception cref="InvalidOperationException">At least one reported row was unusable.</exception>
    protected void ThrowIfTorrentListCollapsed(int reportedCount, int usableCount)
    {
        if (reportedCount == usableCount)
        {
            return;
        }

        throw new InvalidOperationException($"{_downloadClientConfig.Name} reported {reportedCount} torrents, only {usableCount} usable");
    }

    protected async Task<IReadOnlyList<string>> BuildClaimedPathsAsync(
        IReadOnlyList<ITorrentItemWrapper> torrents,
        Func<ITorrentItemWrapper, Task<IReadOnlyCollection<string>>> resolveRelativeFilePaths)
    {
        HashSet<string> claimed = new(StringComparer.OrdinalIgnoreCase);

        foreach (ITorrentItemWrapper torrent in torrents)
        {
            IReadOnlyCollection<string> relativeFilePaths;
            try
            {
                relativeFilePaths = await resolveRelativeFilePaths(torrent);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "failed to resolve files, falling back to name | {name}", torrent.Name);
                relativeFilePaths = [];
            }

            foreach (string path in BuildClaimedPaths(torrent, relativeFilePaths))
            {
                claimed.Add(path);
            }
        }

        return claimed.ToList();
    }

    /// <summary>
    /// The top-level entries a torrent occupies.
    /// </summary>
    private IReadOnlyList<string> BuildClaimedPaths(ITorrentItemWrapper torrent, IReadOnlyCollection<string> relativeFilePaths)
    {
        List<string> claimed = [];
        if (string.IsNullOrEmpty(torrent.SavePath))
        {
            return claimed;
        }

        claimed.Add(RemapAndTrim(torrent.SavePath));

        IReadOnlyCollection<string> sources = relativeFilePaths;
        if (sources.Count == 0 && !string.IsNullOrEmpty(torrent.Name))
        {
            sources = [torrent.Name];
        }

        foreach (string relativePath in sources)
        {
            string firstSegment = FirstSegment(relativePath);
            if (!string.IsNullOrEmpty(firstSegment))
            {
                claimed.Add(RemapAndTrim(Path.Combine(torrent.SavePath, firstSegment)));
            }
        }

        return claimed;
    }

    private static string FirstSegment(string relativePath)
    {
        string[] parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    protected string RemapAndTrim(string path) =>
        PathHelper
            .NormalizeAndRemap(path, _downloadClientConfig.DownloadDirectorySource, _downloadClientConfig.DownloadDirectoryTarget)
            .TrimEnd(Path.DirectorySeparatorChar);

    /// <inheritdoc/>
    public abstract List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules);

    /// <inheritdoc/>
    public abstract List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig);

    /// <inheritdoc/>
    public virtual async Task CleanDownloadsAsync(List<ITorrentItemWrapper>? downloads, List<ISeedingRule> seedingRules)
    {
        if (downloads?.Count is null or 0)
        {
            return;
        }

        foreach (ITorrentItemWrapper torrent in downloads)
        {
            if (string.IsNullOrEmpty(torrent.Hash))
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
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
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

    /// <inheritdoc/>
    public abstract Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig);

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

    /// <summary>
    /// Counts unwanted files and collects the indices to block.
    /// Don't yield a file the client excludes from the count, such as one with no index.
    /// Deluge and rTorrent validate the bare filename but log the relative path.
    /// With <paramref name="deleteIfAnyFileBlocked"/> on, the first unwanted file sets <c>DeleteImmediately</c> and ends the scan.
    /// </summary>
    protected (List<int> UnwantedIndices, long TotalFiles, long TotalUnwantedFiles, bool DeleteImmediately) ScanFilesForBlocking(
        IEnumerable<(int Index, string ValidationName, string LogName, FileBlockAction Action)> files,
        BlocklistType blocklistType,
        ConcurrentBag<string> patterns,
        ConcurrentBag<Regex> regexes,
        bool deleteIfAnyFileBlocked)
    {
        List<int> unwantedIndices = [];
        long totalFiles = 0;
        long totalUnwantedFiles = 0;

        foreach ((int index, string validationName, string logName, FileBlockAction action) in files)
        {
            totalFiles++;

            if (action is FileBlockAction.AlreadySkipped)
            {
                _logger.LogTrace("File is already skipped | {File}", logName);
                totalUnwantedFiles++;
                continue;
            }

            if (_filenameEvaluator.IsValid(validationName, blocklistType, patterns, regexes))
            {
                _logger.LogTrace("File is valid | {File}", logName);
                continue;
            }

            _logger.LogInformation("unwanted file found | {File}", logName);
            totalUnwantedFiles++;

            if (deleteIfAnyFileBlocked)
            {
                return (unwantedIndices, totalFiles, totalUnwantedFiles, true);
            }

            unwantedIndices.Add(index);
        }

        return (unwantedIndices, totalFiles, totalUnwantedFiles, false);
    }

    /// <inheritdoc/>
    public abstract Task ChangeTorrentCategoryAsync(ITorrentItemWrapper torrent, string targetCategory, bool useTag);

    /// <inheritdoc/>
    public abstract Task CreateCategoryAsync(string name);

    /// <inheritdoc/>
    public abstract Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads);

    /// <summary>
    /// Deletes the specified download from the download client.
    /// Each client implementation handles the deletion according to its API requirements.
    /// </summary>
    /// <param name="torrent">The torrent to delete</param>
    /// <param name="deleteSourceFiles">Whether to delete the source files along with the torrent</param>
    public abstract Task DeleteDownload(ITorrentItemWrapper torrent, bool deleteSourceFiles);

    /// <summary>
    /// Stops the specified download in the download client, leaving it there.
    /// Each client implementation handles the stop according to its API requirements.
    /// </summary>
    /// <param name="torrent">The torrent to stop</param>
    public abstract Task StopDownload(ITorrentItemWrapper torrent);
    
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
    
    protected bool TryDeleteFiles(string path, bool failOnNotFound)
    {
        if (string.IsNullOrEmpty(path))
        {
            _logger.LogTrace("File path is null or empty");
            
            if (failOnNotFound)
            {
                return false;
            }

            return true;
        }

        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete directory: {path}", path);
                return false;
            }
        }

        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file: {path}", path);
                return false;
            }
        }
        
        _logger.LogTrace("File path to delete not found: {path}", path);

        if (failOnNotFound)
        {
            return false;
        }

        return true;
    }
}
