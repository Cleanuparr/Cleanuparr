using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

public class HealthCheckResult
{
    public bool IsHealthy { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

public abstract class DownloadService : IDownloadService
{
    protected readonly ILogger<DownloadService> _logger;
    protected readonly IMemoryCache _cache;
    protected readonly IFilenameEvaluator _filenameEvaluator;
    protected readonly IStriker _striker;
    protected readonly MemoryCacheEntryOptions _cacheOptions;
    protected readonly IDryRunInterceptor _dryRunInterceptor;
    protected readonly IHardLinkFileService _hardLinkFileService;
    protected readonly IEventPublisher _eventPublisher;
    protected readonly IBlocklistProvider _blocklistProvider;
    protected readonly HttpClient _httpClient;
    protected readonly DownloadClientConfig _downloadClientConfig;
    protected readonly IRuleEvaluator _ruleEvaluator;
    protected readonly IRuleManager _ruleManager;

    protected DownloadService(
        ILogger<DownloadService> logger,
        IMemoryCache cache,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        IEventPublisher eventPublisher,
        IBlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        IRuleEvaluator ruleEvaluator,
        IRuleManager ruleManager
    )
    {
        _logger = logger;
        _cache = cache;
        _filenameEvaluator = filenameEvaluator;
        _striker = striker;
        _dryRunInterceptor = dryRunInterceptor;
        _hardLinkFileService = hardLinkFileService;
        _eventPublisher = eventPublisher;
        _blocklistProvider = blocklistProvider;
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(StaticConfiguration.TriggerValue + Constants.CacheLimitBuffer);
        _downloadClientConfig = downloadClientConfig;
        _httpClient = httpClientProvider.CreateClient(downloadClientConfig);
        _ruleEvaluator = ruleEvaluator;
        _ruleManager = ruleManager;
    }
    
    public DownloadClientConfig ClientConfig => _downloadClientConfig;

    public abstract void Dispose();

    public abstract Task LoginAsync();

    public abstract Task<HealthCheckResult> HealthCheckAsync();

    public abstract Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash, IReadOnlyList<string> ignoredDownloads);

    /// <inheritdoc/>
    public abstract Task DeleteDownload(string hash, bool deleteSourceFiles);

    /// <inheritdoc/>
    public abstract Task<List<ITorrentItemWrapper>> GetSeedingDownloads();

    /// <inheritdoc/>
    public abstract List<ITorrentItemWrapper>? FilterDownloadsToBeCleanedAsync(List<ITorrentItemWrapper>? downloads, List<SeedingRule> seedingRules);

    /// <inheritdoc/>
    public abstract List<ITorrentItemWrapper>? FilterDownloadsToChangeCategoryAsync(List<ITorrentItemWrapper>? downloads, List<string> categories);

    /// <inheritdoc/>
    public virtual async Task CleanDownloadsAsync(List<ITorrentItemWrapper>? downloads, List<SeedingRule> seedingRules)
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

            SeedingRule? category = seedingRules
                .FirstOrDefault(x => (torrent.Category ?? string.Empty).Equals(x.Name, StringComparison.InvariantCultureIgnoreCase));

            if (category is null)
            {
                continue;
            }

            var downloadCleanerConfig = ContextProvider.Get<DownloadCleanerConfig>(nameof(DownloadCleanerConfig));

            if (!downloadCleanerConfig.DeletePrivate && torrent.IsPrivate)
            {
                _logger.LogDebug("skip | download is private | {name}", torrent.Name);
                continue;
            }

            ContextProvider.Set(ContextProvider.Keys.DownloadName, torrent.Name);
            ContextProvider.Set(ContextProvider.Keys.Hash, torrent.Hash);
            ContextProvider.Set(ContextProvider.Keys.DownloadClientUrl, _downloadClientConfig.ExternalOrInternalUrl);

            TimeSpan seedingTime = TimeSpan.FromSeconds(torrent.SeedingTimeSeconds);
            SeedingCheckResult result = ShouldCleanDownload(torrent.Ratio, seedingTime, category);

            if (!result.ShouldClean)
            {
                continue;
            }

            await _dryRunInterceptor.InterceptAsync(() => DeleteDownloadInternal(torrent, category.DeleteSourceFiles));

            _logger.LogInformation(
                "download cleaned | {reason} reached | delete files: {deleteFiles} | {name}",
                result.Reason is CleanReason.MaxRatioReached
                    ? "MAX_RATIO & MIN_SEED_TIME"
                    : "MAX_SEED_TIME",
                category.DeleteSourceFiles,
                torrent.Name
            );

            await _eventPublisher.PublishDownloadCleaned(torrent.Ratio, seedingTime, category.Name, result.Reason);
        }
    }

    /// <inheritdoc/>
    public abstract Task ChangeCategoryForNoHardLinksAsync(List<ITorrentItemWrapper>? downloads);

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
    protected abstract Task DeleteDownloadInternal(ITorrentItemWrapper torrent, bool deleteSourceFiles);
    
    protected SeedingCheckResult ShouldCleanDownload(double ratio, TimeSpan seedingTime, SeedingRule category)
    {
        // check ratio
        if (DownloadReachedRatio(ratio, seedingTime, category))
        {
            return new()
            {
                ShouldClean = true,
                Reason = CleanReason.MaxRatioReached
            };
        }
            
        // check max seed time
        if (DownloadReachedMaxSeedTime(seedingTime, category))
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
    
    private bool DownloadReachedRatio(double ratio, TimeSpan seedingTime, SeedingRule category)
    {
        if (category.MaxRatio < 0)
        {
            return false;
        }
        
        string downloadName = ContextProvider.Get<string>(ContextProvider.Keys.DownloadName);
        TimeSpan minSeedingTime = TimeSpan.FromHours(category.MinSeedTime);
        
        if (category.MinSeedTime > 0 && seedingTime < minSeedingTime)
        {
            _logger.LogDebug("skip | download has not reached MIN_SEED_TIME | {name}", downloadName);
            return false;
        }

        if (ratio < category.MaxRatio)
        {
            _logger.LogDebug("skip | download has not reached MAX_RATIO | {name}", downloadName);
            return false;
        }
        
        // max ratio is 0 or reached
        return true;
    }
    
    private bool DownloadReachedMaxSeedTime(TimeSpan seedingTime, SeedingRule category)
    {
        if (category.MaxSeedTime < 0)
        {
            return false;
        }
        
        string downloadName = ContextProvider.Get<string>(ContextProvider.Keys.DownloadName);
        TimeSpan maxSeedingTime = TimeSpan.FromHours(category.MaxSeedTime);
        
        if (category.MaxSeedTime > 0 && seedingTime < maxSeedingTime)
        {
            _logger.LogDebug("skip | download has not reached MAX_SEED_TIME | {name}", downloadName);
            return false;
        }

        // max seed time is 0 or reached
        return true;
    }
}