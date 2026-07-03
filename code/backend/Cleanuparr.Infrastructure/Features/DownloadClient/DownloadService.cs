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
        ISeedingRuleEvaluator seedingRuleEvaluator
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
    }
    
    public DownloadClientConfig ClientConfig => _downloadClientConfig;

    protected void SetDownloadClientContext()
    {
        ContextProvider.SetDownloadClient(_downloadClientConfig);
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
                _logger.LogTrace("No seeding rules matched | {name}", torrent.Name);
                continue;
            }
            
            _logger.LogTrace("Seeding rule matched | {seedingRule} | {name}", seedingRule.Name, torrent.Name);

            ContextProvider.Set(ContextProvider.Keys.ItemName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
            SetDownloadClientContext();

            TimeSpan seedingTime = TimeSpan.FromSeconds(torrent.SeedingTimeSeconds);
            SeedingCheckResult result = ShouldCleanDownload(torrent.Ratio, seedingTime, torrent.SeederCount, seedingRule);

            if (!result.ShouldClean)
            {
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(() => DeleteDownload(torrent, seedingRule.DeleteSourceFiles));

            _logger.LogInformation(
                "download cleaned | {reason} reached | delete files: {deleteFiles} | {name}",
                result.Reason is CleanReason.MaxRatioReached
                    ? "MAX_RATIO & MIN_SEED_TIME"
                    : "MAX_SEED_TIME",
                seedingRule.DeleteSourceFiles,
                torrent.Name
            );

            await _eventPublisher.PublishDownloadCleaned(torrent.Ratio, seedingTime, torrent.Category ?? string.Empty, result.Reason);
        }
    }

    /// <inheritdoc/>
    public abstract Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads, UnlinkedConfig unlinkedConfig);

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
    
    private SeedingCheckResult ShouldCleanDownload(double ratio, TimeSpan seedingTime, int? seederCount, ISeedingRule seedingRule)
    {
        if (BelowMinimumSeeders(seederCount, seedingRule))
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
    
    protected string? GetRootWithFirstDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string? root = Path.GetPathRoot(path);
        
        if (root is null)
        {
            return null;
        }

        string relativePath = path[root.Length..].TrimStart(Path.DirectorySeparatorChar);
        string[] parts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 0 ? Path.Combine(root, parts[0]) : root;
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
