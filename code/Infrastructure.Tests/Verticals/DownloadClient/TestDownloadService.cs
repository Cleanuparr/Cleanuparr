using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Common.Configuration.ContentBlocker;
using Common.Configuration.DownloadCleaner;
using Common.Configuration.QueueCleaner;
using Infrastructure.Interceptors;
using Infrastructure.Verticals.ContentBlocker;
using Infrastructure.Verticals.DownloadClient;
using Infrastructure.Verticals.Files;
using Infrastructure.Verticals.ItemStriker;
using Infrastructure.Verticals.Notifications;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tests.Verticals.DownloadClient;

public class TestDownloadService : DownloadService
{
    public TestDownloadService(
        ILogger<DownloadService> logger,
        IOptions<QueueCleanerConfig> queueCleanerConfig,
        IOptions<ContentBlockerConfig> contentBlockerConfig,
        IOptions<DownloadCleanerConfig> downloadCleanerConfig,
        IMemoryCache cache,
        IFilenameEvaluator filenameEvaluator,
        IStriker striker,
        INotificationPublisher notifier,
        IDryRunInterceptor dryRunInterceptor,
        IHardlinkFileService hardlinkFileService
    ) : base(
        logger, queueCleanerConfig, contentBlockerConfig, downloadCleanerConfig, cache,
        filenameEvaluator, striker, notifier, dryRunInterceptor, hardlinkFileService
    )
    {
    }

    public override void Dispose() { }
    public override Task LoginAsync() => Task.CompletedTask;
    public override Task<StalledResult> ShouldRemoveFromArrQueueAsync(string hash) => Task.FromResult(new StalledResult());
    public override Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, BlocklistType blocklistType, 
        ConcurrentBag<string> patterns, ConcurrentBag<Regex> regexes) => Task.FromResult(new BlockFilesResult());

    public override Task DeleteDownload(string hash) => Task.CompletedTask;
    public override Task<List<object>?> GetDownloadsToBeCleaned(List<CleanCategory> categories) => Task.FromResult<List<object>?>(null);
    public override Task<List<object>?> GetDownloadsToChangeCategory(List<string> categories) => Task.FromResult<List<object>?>(null);
    public override Task CleanDownloads(List<object> downloads, List<CleanCategory> categoriesToClean, HashSet<string> excludedHashes) => Task.CompletedTask;
    public override Task ChangeCategoryForNoHardlinksAsync(List<object> downloads, HashSet<string> excludedHashes) => Task.CompletedTask;
    
    // Expose protected methods for testing
    public new void ResetStrikesOnProgress(string hash, long downloaded) => base.ResetStrikesOnProgress(hash, downloaded);
    public new Task<bool> StrikeAndCheckLimit(string hash, string itemName) => base.StrikeAndCheckLimit(hash, itemName);
    public new SeedingCheckResult ShouldCleanDownload(double ratio, TimeSpan seedingTime, CleanCategory category) => base.ShouldCleanDownload(ratio, seedingTime, category);
}