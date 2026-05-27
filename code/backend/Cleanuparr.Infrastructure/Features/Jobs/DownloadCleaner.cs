using System.IO.Enumeration;

using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Shared.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LogContext = Serilog.Context.LogContext;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class DownloadCleaner : GenericHandler
{
    private readonly HashSet<string> _downloadsProcessedByArrs = [];
    private readonly TimeProvider _timeProvider;
    private readonly IHardLinkFileService _hardLinkFileService;
    private readonly IDryRunInterceptor _dryRunInterceptor;

    public DownloadCleaner(
        ILogger<DownloadCleaner> logger,
        DataContext dataContext,
        IMemoryCache cache,
        IBus messageBus,
        IArrClientFactory arrClientFactory,
        IArrQueueIterator arrArrQueueIterator,
        IDownloadServiceFactory downloadServiceFactory,
        IEventPublisher eventPublisher,
        TimeProvider timeProvider,
        IHardLinkFileService hardLinkFileService,
        IDryRunInterceptor dryRunInterceptor
    ) : base(
        logger, dataContext, cache, messageBus,
        arrClientFactory, arrArrQueueIterator, downloadServiceFactory, eventPublisher
    )
    {
        _timeProvider = timeProvider;
        _hardLinkFileService = hardLinkFileService;
        _dryRunInterceptor = dryRunInterceptor;
    }

    protected override async Task ExecuteInternalAsync(CancellationToken cancellationToken = default)
    {
        var downloadServices = await GetInitializedDownloadServicesAsync();

        if (downloadServices.Count is 0)
        {
            _logger.LogWarning("Processing skipped because no download clients are configured");
            return;
        }

        var config = ContextProvider.Get<DownloadCleanerConfig>();

        List<string> ignoredDownloads = ContextProvider.Get<GeneralConfig>(nameof(GeneralConfig)).IgnoredDownloads;
        ignoredDownloads.AddRange(config.IgnoredDownloads);

        var downloadServiceToDownloadsMap = new Dictionary<IDownloadService, List<ITorrentItemWrapper>>();
        var loggedInServices = new List<IDownloadService>();
        var failedClientIds = new HashSet<Guid>();

        foreach (var downloadService in downloadServices)
        {
            using var dcType = LogContext.PushProperty(LogProperties.DownloadClientType, downloadService.ClientConfig.Type.ToString());
            using var dcName = LogContext.PushProperty(LogProperties.DownloadClientName, downloadService.ClientConfig.Name);

            try
            {
                await downloadService.LoginAsync();
                loggedInServices.Add(downloadService);
                List<ITorrentItemWrapper> clientDownloads = await downloadService.GetSeedingDownloads();

                if (clientDownloads.Count > 0)
                {
                    downloadServiceToDownloadsMap[downloadService] = clientDownloads;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get seeding downloads from download client {clientName}", downloadService.ClientConfig.Name);
                failedClientIds.Add(downloadService.ClientConfig.Id);
            }
        }

        int totalDownloads = downloadServiceToDownloadsMap.Values.Sum(x => x.Count);
        _logger.LogTrace("Found {count} seeding downloads across {clientCount} clients", totalDownloads, downloadServiceToDownloadsMap.Count);

        if (downloadServiceToDownloadsMap.Count > 0)
        {
            // wait for the downloads to appear in the arr queue
            await Task.Delay(TimeSpan.FromSeconds(10), _timeProvider, cancellationToken);

            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Sonarr)), true);
            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Radarr)), true);
            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Lidarr)), true);
            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Readarr)), true);
            await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Whisparr)), true);

            foreach (var pair in downloadServiceToDownloadsMap)
            {
                List<ITorrentItemWrapper> filteredDownloads = [];

                foreach (ITorrentItemWrapper download in pair.Value)
                {
                    if (download.IsIgnored(ignoredDownloads))
                    {
                        _logger.LogDebug("skip | download is ignored | {name}", download.Name);
                        continue;
                    }

                    if (_downloadsProcessedByArrs.Any(x => x.Equals(download.Hash, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        _logger.LogDebug("skip | download is used by an arr | {name}", download.Name);
                        continue;
                    }

                    filteredDownloads.Add(download);
                }

                downloadServiceToDownloadsMap[pair.Key] = filteredDownloads;
            }

            // Process each client with its own per-client config
            foreach (var (downloadService, clientDownloads) in downloadServiceToDownloadsMap)
            {
                using var dcType = LogContext.PushProperty(LogProperties.DownloadClientType, downloadService.ClientConfig.Type.ToString());
                using var dcName = LogContext.PushProperty(LogProperties.DownloadClientName, downloadService.ClientConfig.Name);

                var seedingRules = await LoadSeedingRulesForClientAsync(downloadService.ClientConfig);
                var unlinkedConfig = await LoadUnlinkedConfigForClientAsync(downloadService.ClientConfig.Id);

                if (unlinkedConfig is { Enabled: true })
                {
                    if (unlinkedConfig.Categories.Count > 0)
                    {
                        await ChangeUnlinkedCategoriesForClientAsync(downloadService, clientDownloads, unlinkedConfig);
                    }
                    else
                    {
                        _logger.LogWarning("Unlinked config is enabled but no categories are configured for {name}, skipping", downloadService.ClientConfig.Name);
                    }
                }

                if (seedingRules.Count > 0)
                {
                    await CleanDownloadsForClientAsync(downloadService, clientDownloads, seedingRules);
                }
            }
        }
        else
        {
            _logger.LogInformation("No seeding downloads found, skipping seeding-rule and unlinked-category processing");
        }

        try
        {
            await ProcessOrphanedFilesAsync(loggedInServices, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process orphaned files");
        }

        foreach (var downloadService in downloadServices)
        {
            downloadService.Dispose();
        }
    }

    protected override async Task ProcessInstanceAsync(ArrInstance instance)
    {
        using IDisposable _ = LogContext.PushProperty(LogProperties.Category, instance.ArrConfig.Type.ToString());
        using IDisposable _2 = LogContext.PushProperty(LogProperties.InstanceName, instance.Name);

        IArrClient arrClient = _arrClientFactory.GetClient(instance.ArrConfig.Type, instance.Version);

        await _arrArrQueueIterator.Iterate(arrClient, instance, items =>
        {
            var groups = items
                .Where(x => !string.IsNullOrEmpty(x.DownloadId))
                .GroupBy(x => x.DownloadId)
                .ToList();

            foreach (QueueRecord record in groups.Select(group => group.First()))
            {
                _downloadsProcessedByArrs.Add(record.DownloadId.ToLowerInvariant());
            }

            return Task.CompletedTask;
        });
    }

    private async Task ChangeUnlinkedCategoriesForClientAsync(
        IDownloadService downloadService,
        List<ITorrentItemWrapper> clientDownloads,
        UnlinkedConfig unlinkedConfig)
    {
        if (unlinkedConfig.IgnoredRootDirs.Count > 0)
        {
            _hardLinkFileService.PopulateFileCounts(unlinkedConfig.IgnoredRootDirs);
        }

        try
        {
            var downloadsToChangeCategory = downloadService
                .FilterDownloadsToChangeCategoryAsync(clientDownloads, unlinkedConfig);

            if (downloadsToChangeCategory?.Count is null or 0)
            {
                return;
            }

            _logger.LogInformation("Evaluating {count} downloads for hardlinks", downloadsToChangeCategory.Count);

            try
            {
                await downloadService.CreateCategoryAsync(unlinkedConfig.TargetCategory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create category {category}", unlinkedConfig.TargetCategory);
            }

            await downloadService.ChangeCategoryForNoHardLinksAsync(downloadsToChangeCategory, unlinkedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process unlinked downloads for {clientName}", downloadService.ClientConfig.Name);
        }

        _logger.LogInformation("Finished hardlinks evaluation");
    }

    private async Task CleanDownloadsForClientAsync(
        IDownloadService downloadService,
        List<ITorrentItemWrapper> clientDownloads,
        List<ISeedingRule> seedingRules)
    {
        try
        {
            var downloadsToClean = downloadService
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

    private async Task<List<ISeedingRule>> LoadSeedingRulesForClientAsync(DownloadClientConfig clientConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            return clientConfig.TypeName switch
            {
                DownloadClientTypeName.qBittorrent => (await _dataContext.QBitSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.Deluge => (await _dataContext.DelugeSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.Transmission => (await _dataContext.TransmissionSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.uTorrent => (await _dataContext.UTorrentSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                DownloadClientTypeName.rTorrent => (await _dataContext.RTorrentSeedingRules
                    .Where(r => r.DownloadClientConfigId == clientConfig.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
                _ => []
            };
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task<UnlinkedConfig?> LoadUnlinkedConfigForClientAsync(Guid clientId)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            return await _dataContext.UnlinkedConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.DownloadClientConfigId == clientId);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task<List<OrphanedFilesConfig>> LoadOrphanedFilesConfigsAsync(CancellationToken cancellationToken)
    {
        await DataContext.Lock.WaitAsync(cancellationToken);
        try
        {
            return await _dataContext.OrphanedFilesConfigs
                .AsNoTracking()
                .Include(x => x.DownloadClientConfig)
                .Where(x => x.Enabled && x.DownloadClientConfig.Enabled)
                .ToListAsync(cancellationToken);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task ProcessOrphanedFilesAsync(IReadOnlyList<IDownloadService> downloadServices, CancellationToken cancellationToken)
    {
        List<OrphanedFilesConfig> orphanedFilesConfigs = await LoadOrphanedFilesConfigsAsync(cancellationToken);

        if (orphanedFilesConfigs.Count is 0)
        {
            _logger.LogDebug("No orphaned files settings have been configured");
            return;
        }

        // Build set of all content paths claimed by active torrents across ALL download clients
        // to avoid false positives from cross-seeded clients.
        HashSet<string> claimedPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (IDownloadService downloadService in downloadServices)
        {
            var downloadClient = downloadService.ClientConfig;
            try
            {
                var torrents = await downloadService.GetAllTorrentsLite();

                foreach (var torrent in torrents)
                {
                    if (string.IsNullOrEmpty(torrent.SavePath))
                    {
                        continue;
                    }

                    string remappedSavePath = PathHelper.NormalizeAndRemap(
                        torrent.SavePath,
                        downloadClient.DownloadDirectorySource,
                        downloadClient.DownloadDirectoryTarget
                    ).TrimEnd(Path.DirectorySeparatorChar);

                    claimedPaths.Add(remappedSavePath);

                    if (string.IsNullOrEmpty(torrent.Name))
                    {
                        continue;
                    }

                    string contentPath = PathHelper.NormalizeAndRemap(
                        Path.Combine(torrent.SavePath, torrent.Name),
                        downloadClient.DownloadDirectorySource,
                        downloadClient.DownloadDirectoryTarget
                    );

                    claimedPaths.Add(contentPath.TrimEnd(Path.DirectorySeparatorChar));
                }

                _logger.LogDebug("Loaded {count} torrent paths from {name}", torrents.Count, downloadClient.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get torrents from client {name}", downloadClient.Name);
            }
        }

        _logger.LogDebug("{count} claimed paths across all clients", claimedPaths.Count);

        foreach (OrphanedFilesConfig clientConfig in orphanedFilesConfigs)
        {
            if (clientConfig.ScanDirectories.Count is 0)
            {
                _logger.LogWarning(
                    "skip | no scan directories configured for client {name}",
                    clientConfig.DownloadClientConfig.Name);
                continue;
            }

            string normalizedOrphanedDir = Path.GetFullPath(clientConfig.OrphanedDirectory).TrimEnd(Path.DirectorySeparatorChar);

            foreach (var scanDir in clientConfig.ScanDirectories)
            {
                if (!Directory.Exists(scanDir))
                {
                    _logger.LogWarning("Scan directory does not exist: {dir}", scanDir);
                    continue;
                }

                _logger.LogDebug("Scanning {dir}", scanDir);

                try
                {
                    ProcessDirectory(scanDir, claimedPaths, clientConfig, normalizedOrphanedDir, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning {dir} for client {name}", scanDir, clientConfig.DownloadClientConfig.Name);
                }
            }

            PurgeOrphanedDirectory(clientConfig, cancellationToken);
        }
    }

    private void ProcessDirectory(
        string directory,
        HashSet<string> claimedPaths,
        OrphanedFilesConfig clientConfig,
        string normalizedOrphanedDir,
        CancellationToken cancellationToken)
    {
        foreach (string filePath in Directory.EnumerateFileSystemEntries(directory, "*", new EnumerationOptions { RecurseSubdirectories = false }))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string normalizedPath = Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar);

                // Skip reparse points (symlinks/junctions)
                if ((File.GetAttributes(normalizedPath) & FileAttributes.ReparsePoint) != 0)
                {
                    _logger.LogWarning("skip | reparse point | {path}", normalizedPath);
                    continue;
                }

                if (normalizedPath.Equals(normalizedOrphanedDir, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("skip | orphaned directory itself | {path}", normalizedPath);
                    continue;
                }

                if (claimedPaths.Contains(normalizedPath))
                {
                    _logger.LogDebug("skip | claimed by torrent | {path}", normalizedPath);
                    continue;
                }

                string entryName = Path.GetFileName(normalizedPath);
                if (clientConfig.ExcludePatterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, entryName, ignoreCase: true)))
                {
                    _logger.LogDebug("skip | excluded by pattern | {path}", normalizedPath);
                    continue;
                }

                if (clientConfig.MinFileAgeHours > 0)
                {
                    DateTime lastWrite = File.GetLastWriteTimeUtc(normalizedPath);
                    DateTime created = File.GetCreationTimeUtc(normalizedPath);
                    DateTime mostRecent = lastWrite > created ? lastWrite : created;
                    double ageHours = (_timeProvider.GetUtcNow().UtcDateTime - mostRecent).TotalHours;

                    if (ageHours < clientConfig.MinFileAgeHours)
                    {
                        _logger.LogDebug(
                            "skip | too recent ({age:F1}h < {min}h) | {path}",
                            ageHours, clientConfig.MinFileAgeHours, normalizedPath);
                        continue;
                    }
                }

                _logger.LogInformation("orphaned entry found | {path}", normalizedPath);

                string capturedEntry = normalizedPath;
                string capturedOrphanedDir = normalizedOrphanedDir;
                _dryRunInterceptor.Intercept(() => MoveToOrphanedDirectory(capturedEntry, capturedOrphanedDir));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle orphaned entry: {path}", filePath);
            }
        }
    }

    private void MoveToOrphanedDirectory(string path, string orphanedDirectory)
    {
        string entryName = Path.GetFileName(path);
        string destination = Path.Combine(orphanedDirectory, entryName);

        if (Path.Exists(destination))
        {
            const int maxAttempts = 100;
            string timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmmss");
            destination = Path.Combine(orphanedDirectory, $"{entryName}_{timestamp}");

            int counter = 1;
            while (Path.Exists(destination))
            {
                if (counter > maxAttempts)
                {
                    throw new InvalidOperationException($"Could not find a free destination name for orphaned entry after {maxAttempts} attempts: {path}");
                }
                
                destination = Path.Combine(orphanedDirectory, $"{entryName}_{timestamp}_{counter}");
                counter++;
            }
        }

        Directory.CreateDirectory(orphanedDirectory);

        if (Directory.Exists(path))
        {
            Directory.Move(path, destination);
        }
        else
        {
            File.Move(path, destination);
        }

        File.SetLastWriteTimeUtc(destination, _timeProvider.GetUtcNow().UtcDateTime);

        _logger.LogInformation("orphaned entry moved | {source} -> {dest}", path, destination);
    }

    private void PurgeOrphanedDirectory(OrphanedFilesConfig clientConfig, CancellationToken cancellationToken)
    {
        if (!clientConfig.PurgeAfterHours.HasValue)
        {
            return;
        }

        if (!Directory.Exists(clientConfig.OrphanedDirectory))
        {
            return;
        }

        DateTime cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-clientConfig.PurgeAfterHours.Value);

        foreach (var filePath in Directory.EnumerateFileSystemEntries(clientConfig.OrphanedDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            DateTime lastWrite = File.GetLastWriteTimeUtc(filePath);
            if (lastWrite > cutoff)
            {
                continue;
            }

            try
            {
                int hours = clientConfig.PurgeAfterHours.Value;

                if (Directory.Exists(filePath))
                {
                    Directory.Delete(filePath, recursive: true);
                }
                else
                {
                    File.Delete(filePath);
                }

                _logger.LogInformation("Purged old orphaned entry ({hours}h+) | {path}", hours, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge orphaned entry: {path}", filePath);
            }
        }
    }
}
