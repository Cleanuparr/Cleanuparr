using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Cache;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.ContentBlocker;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Cleanuparr.Shared.Helpers;
using Infrastructure.Interceptors;
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
    protected readonly EventPublisher _eventPublisher;
    protected readonly BlocklistProvider _blocklistProvider;
    protected readonly HttpClient _httpClient;
    protected readonly DownloadClientConfig _downloadClientConfig;
    
    protected DownloadService(
        ILogger<DownloadService> logger,
        IMemoryCache cache,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor,
        IHardLinkFileService hardLinkFileService,
        IDynamicHttpClientProvider httpClientProvider,
        EventPublisher eventPublisher,
        BlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig
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
    }
    
    public DownloadClientConfig ClientConfig => _downloadClientConfig;

    public abstract void Dispose();

    public abstract Task LoginAsync();

    public abstract Task<HealthCheckResult> HealthCheckAsync();

    public abstract Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string hash,
        IReadOnlyList<string> ignoredDownloads);

    /// <inheritdoc/>
    public abstract Task DeleteDownload(string hash);

    /// <inheritdoc/>
    public abstract Task<List<object>?> GetSeedingDownloads();
    
    /// <inheritdoc/>
    public abstract List<object>? FilterDownloadsToBeCleanedAsync(List<object>? downloads, List<CleanCategory> categories);

    /// <inheritdoc/>
    public abstract List<object>? FilterDownloadsToChangeCategoryAsync(List<object>? downloads, List<string> categories);

    /// <inheritdoc/>
    public abstract Task CleanDownloadsAsync(List<object>? downloads, List<CleanCategory> categoriesToClean, HashSet<string> excludedHashes, IReadOnlyList<string> ignoredDownloads);

    /// <inheritdoc/>
    public abstract Task ChangeCategoryForNoHardLinksAsync(List<object>? downloads, HashSet<string> excludedHashes, IReadOnlyList<string> ignoredDownloads);
    
    /// <inheritdoc/>
    public abstract Task CreateCategoryAsync(string name);
    
    /// <inheritdoc/>
    public abstract Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads);

    protected bool IsDefinitelyMalware(string filename)
    {
        if (filename.Contains("thepirateheaven.org", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }
    
    protected void ResetStalledStrikesOnProgress(string hash, long downloaded)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));

        if (!queueCleanerConfig.Stalled.ResetStrikesOnProgress)
        {
            return;
        }

        if (_cache.TryGetValue(CacheKeys.StrikeItem(hash, StrikeType.Stalled), out StalledCacheItem? cachedItem) &&
            cachedItem is not null && downloaded > cachedItem.Downloaded)
        {
            // cache item found
            _cache.Remove(CacheKeys.Strike(StrikeType.Stalled, hash));
            _logger.LogDebug("resetting stalled strikes for {hash} due to progress", hash);
        }
        
        _cache.Set(CacheKeys.StrikeItem(hash, StrikeType.Stalled), new StalledCacheItem { Downloaded = downloaded }, _cacheOptions);
    }
    
    protected void ResetSlowSpeedStrikesOnProgress(string downloadName, string hash)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Slow.ResetStrikesOnProgress)
        {
            return;
        }

        string key = CacheKeys.Strike(StrikeType.SlowSpeed, hash);

        if (!_cache.TryGetValue(key, out object? value) || value is null)
        {
            return;
        }
        
        _cache.Remove(key);
        _logger.LogDebug("resetting slow speed strikes due to progress | {name}", downloadName);
    }
    
    protected void ResetSlowTimeStrikesOnProgress(string downloadName, string hash)
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (queueCleanerConfig.Slow.ResetStrikesOnProgress)
        {
            return;
        }

        string key = CacheKeys.Strike(StrikeType.SlowTime, hash);

        if (!_cache.TryGetValue(key, out object? value) || value is null)
        {
            return;
        }
        
        _cache.Remove(key);
        _logger.LogDebug("resetting slow time strikes due to progress | {name}", downloadName);
    }

    protected async Task<(bool ShouldRemove, DeleteReason Reason)> CheckIfSlow(
        string downloadHash,
        string downloadName,
        ByteSize minSpeed,
        ByteSize currentSpeed,
        SmartTimeSpan maxTime,
        SmartTimeSpan currentTime
    )
    {
        var queueCleanerConfig = ContextProvider.Get<QueueCleanerConfig>(nameof(QueueCleanerConfig));
        
        if (minSpeed.Bytes > 0 && currentSpeed < minSpeed)
        {
            _logger.LogTrace("slow speed | {speed}/s | {name}", currentSpeed.ToString(), downloadName);
            
            bool shouldRemove = await _striker
                .StrikeAndCheckLimit(downloadHash, downloadName, queueCleanerConfig.Slow.MaxStrikes, StrikeType.SlowSpeed);

            if (shouldRemove)
            {
                return (true, DeleteReason.SlowSpeed);
            }
        }
        else
        {
            ResetSlowSpeedStrikesOnProgress(downloadName, downloadHash);
        }
        
        if (maxTime.Time > TimeSpan.Zero && currentTime > maxTime)
        {
            _logger.LogTrace("slow estimated time | {time} | {name}", currentTime.ToString(), downloadName);
            
            bool shouldRemove = await _striker
                .StrikeAndCheckLimit(downloadHash, downloadName, queueCleanerConfig.Slow.MaxStrikes, StrikeType.SlowTime);

            if (shouldRemove)
            {
                return (true, DeleteReason.SlowTime);
            }
        }
        else
        {
            ResetSlowTimeStrikesOnProgress(downloadName, downloadHash);
        }
        
        return (false, DeleteReason.None);
    }
    
    protected SeedingCheckResult ShouldCleanDownload(double ratio, TimeSpan seedingTime, CleanCategory category)
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
    
    private bool DownloadReachedRatio(double ratio, TimeSpan seedingTime, CleanCategory category)
    {
        if (category.MaxRatio < 0)
        {
            return false;
        }
        
        string downloadName = ContextProvider.Get<string>("downloadName");
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
        
        // max ration is 0 or reached
        return true;
    }
    
    private bool DownloadReachedMaxSeedTime(TimeSpan seedingTime, CleanCategory category)
    {
        if (category.MaxSeedTime < 0)
        {
            return false;
        }
        
        string downloadName = ContextProvider.Get<string>("downloadName");
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