using System.IO.Enumeration;

using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.OrphanedFilesCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class OrphanedFilesCleaner : IHandler
{
    private readonly ILogger<OrphanedFilesCleaner> _logger;
    private readonly DataContext _dataContext;
    private readonly IDownloadServiceFactory _downloadServiceFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;

    public OrphanedFilesCleaner(
        ILogger<OrphanedFilesCleaner> logger,
        DataContext dataContext,
        IDownloadServiceFactory downloadServiceFactory,
        IEventPublisher eventPublisher,
        IDryRunInterceptor dryRunInterceptor
    )
    {
        _logger = logger;
        _dataContext = dataContext;
        _downloadServiceFactory = downloadServiceFactory;
        _eventPublisher = eventPublisher;
        _dryRunInterceptor = dryRunInterceptor;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await DataContext.Lock.WaitAsync(cancellationToken);
        OrphanedFilesCleanerConfig config;
        List<Cleanuparr.Persistence.Models.Configuration.DownloadClientConfig> downloadClientConfigs;

        try
        {
            config = await _dataContext.OrphanedFilesCleanerConfigs.AsNoTracking().FirstAsync(cancellationToken);
            downloadClientConfigs = await _dataContext.DownloadClients
                .AsNoTracking()
                .Where(x => x.Enabled)
                .ToListAsync(cancellationToken);
        }
        finally
        {
            DataContext.Lock.Release();
        }

        if (!config.Enabled)
        {
            _logger.LogDebug("OrphanedFilesCleaner is disabled, skipping");
            return;
        }

        if (config.ScanDirectories.Count == 0)
        {
            _logger.LogWarning("[OrphanedFilesCleaner] No scan directories configured");
            return;
        }

        bool isDryRun = await _dryRunInterceptor.IsDryRunEnabled();

        // Build set of all content paths claimed by active torrents
        var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasAtLeastOneSupportedClient = false;

        foreach (var clientConfig in downloadClientConfigs)
        {
            IDownloadService? downloadService = null;
            try
            {
                downloadService = _downloadServiceFactory.GetDownloadService(clientConfig);
                await downloadService.LoginAsync();

                var torrents = await downloadService.GetAllTorrents();
                hasAtLeastOneSupportedClient = true;

                foreach (var torrent in torrents)
                {
                    if (string.IsNullOrEmpty(torrent.SavePath))
                    {
                        continue;
                    }

                    string normalizedSavePath = string.Join(
                        Path.DirectorySeparatorChar,
                        torrent.SavePath.Split(['\\', '/'])
                    );

                    string remappedSavePath = PathHelper.RemapPath(
                        normalizedSavePath,
                        config.DownloadDirectorySource,
                        config.DownloadDirectoryTarget
                    ).TrimEnd(Path.DirectorySeparatorChar);

                    // Claim the save_path itself — covers torrents where save_path IS the content directory
                    claimedPaths.Add(remappedSavePath);

                    // Also claim save_path + name — covers torrents that create a named subfolder
                    if (!string.IsNullOrEmpty(torrent.Name))
                    {
                        string rawPathWithName = string.Join(
                            Path.DirectorySeparatorChar,
                            Path.Combine(torrent.SavePath, torrent.Name).Split(['\\', '/'])
                        );

                        string contentPath = PathHelper.RemapPath(
                            rawPathWithName,
                            config.DownloadDirectorySource,
                            config.DownloadDirectoryTarget
                        );

                        claimedPaths.Add(contentPath.TrimEnd(Path.DirectorySeparatorChar));
                    }
                }

                _logger.LogDebug("[OrphanedFilesCleaner] Loaded {count} torrent paths from {name}",
                    torrents.Count, clientConfig.Name);
            }
            catch (NotSupportedException)
            {
                _logger.LogDebug("[OrphanedFilesCleaner] Client {name} does not support orphan detection, skipping",
                    clientConfig.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrphanedFilesCleaner] Failed to get torrents from client {name}", clientConfig.Name);
            }
            finally
            {
                downloadService?.Dispose();
            }
        }

        if (!hasAtLeastOneSupportedClient)
        {
            _logger.LogWarning("[OrphanedFilesCleaner] No configured download client supports orphan detection — aborting scan to avoid false positives");
            return;
        }

        _logger.LogDebug("[OrphanedFilesCleaner] {count} claimed paths across all clients", claimedPaths.Count);

        // Scan configured directories for orphaned entries
        int processedCount = 0;
        foreach (var scanDir in config.ScanDirectories)
        {
            if (processedCount >= config.MaxOrphanedFilesToProcess)
            {
                _logger.LogWarning("[OrphanedFilesCleaner] Reached the limit of {max} orphaned entries per run, stopping scan",
                    config.MaxOrphanedFilesToProcess);
                break;
            }

            if (!Directory.Exists(scanDir))
            {
                _logger.LogWarning("[OrphanedFilesCleaner] Scan directory does not exist: {dir}", scanDir);
                continue;
            }

            _logger.LogDebug("[OrphanedFilesCleaner] Scanning {dir}", scanDir);

            try
            {
                processedCount += await ScanDirectoryAsync(
                    scanDir, claimedPaths, config,
                    config.MaxOrphanedFilesToProcess - processedCount,
                    isDryRun, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrphanedFilesCleaner] Error scanning {dir}", scanDir);
            }
        }

        // Purge old entries from the orphaned directory
        await PurgeOrphanedDirectoryAsync(config, isDryRun, cancellationToken);
    }

    private async Task<int> ScanDirectoryAsync(
        string directory,
        HashSet<string> claimedPaths,
        OrphanedFilesCleanerConfig config,
        int remainingSlots,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        int moved = 0;

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (moved >= remainingSlots)
            {
                break;
            }

            string normalizedEntry = entry.TrimEnd(Path.DirectorySeparatorChar);

            if (claimedPaths.Contains(normalizedEntry))
            {
                _logger.LogDebug("[OrphanedFilesCleaner] skip | claimed by torrent | {path}", normalizedEntry);
                continue;
            }

            // Exclude by glob pattern
            string entryName = Path.GetFileName(normalizedEntry);
            if (config.ExcludePatterns.Any(pattern =>
                    FileSystemName.MatchesSimpleExpression(pattern, entryName, ignoreCase: true)))
            {
                _logger.LogDebug("[OrphanedFilesCleaner] skip | excluded by pattern | {path}", normalizedEntry);
                continue;
            }

            // Skip entries that are too recent
            if (config.MinFileAgeMinutes > 0)
            {
                DateTime lastWrite = File.GetLastWriteTimeUtc(normalizedEntry);
                DateTime created = File.GetCreationTimeUtc(normalizedEntry);
                DateTime mostRecent = lastWrite > created ? lastWrite : created;
                double ageMinutes = (DateTime.UtcNow - mostRecent).TotalMinutes;

                if (ageMinutes < config.MinFileAgeMinutes)
                {
                    _logger.LogDebug(
                        "[OrphanedFilesCleaner] skip | too recent ({age:F1} min < {min} min) | {path}",
                        ageMinutes, config.MinFileAgeMinutes, normalizedEntry);
                    continue;
                }
            }

            _logger.LogInformation("[OrphanedFilesCleaner] orphaned entry found | {path}", normalizedEntry);

            try
            {
                await MoveToOrphanedDirectoryAsync(normalizedEntry, config, isDryRun);
                moved++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrphanedFilesCleaner] Failed to handle orphaned entry: {path}", normalizedEntry);
            }
        }

        return moved;
    }

    private Task MoveToOrphanedDirectoryAsync(string path, OrphanedFilesCleanerConfig config, bool isDryRun)
    {
        if (string.IsNullOrWhiteSpace(config.OrphanedDirectory))
        {
            _logger.LogWarning("[OrphanedFilesCleaner] No orphaned directory configured — {path} was not moved", path);
            return Task.CompletedTask;
        }

        string entryName = Path.GetFileName(path);
        string destination = Path.Combine(config.OrphanedDirectory, entryName);

        if (Path.Exists(destination))
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            destination = Path.Combine(config.OrphanedDirectory, $"{entryName}_{timestamp}");
        }

        if (isDryRun)
        {
            _logger.LogInformation("[DRY RUN] [OrphanedFilesCleaner] would move | {source} -> {dest}", path, destination);
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(config.OrphanedDirectory);

        if (Directory.Exists(path))
        {
            Directory.Move(path, destination);
        }
        else
        {
            File.Move(path, destination);
        }

        _logger.LogInformation("[OrphanedFilesCleaner] orphaned entry moved | {source} -> {dest}", path, destination);
        return Task.CompletedTask;
    }

    private Task PurgeOrphanedDirectoryAsync(OrphanedFilesCleanerConfig config, bool isDryRun, CancellationToken cancellationToken)
    {
        if (!config.EmptyAfterXDays.HasValue)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(config.OrphanedDirectory) || !Directory.Exists(config.OrphanedDirectory))
        {
            return Task.CompletedTask;
        }

        DateTime cutoff = DateTime.UtcNow.AddDays(-config.EmptyAfterXDays.Value);

        foreach (var entry in Directory.EnumerateFileSystemEntries(config.OrphanedDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            DateTime lastWrite = File.GetLastWriteTimeUtc(entry);
            if (lastWrite > cutoff)
            {
                continue;
            }

            if (isDryRun)
            {
                _logger.LogInformation("[DRY RUN] [OrphanedFilesCleaner] would purge old orphaned entry ({days}d+) | {path}",
                    config.EmptyAfterXDays.Value, entry);
                continue;
            }

            try
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }

                _logger.LogInformation("[OrphanedFilesCleaner] purged old orphaned entry ({days}d+) | {path}",
                    config.EmptyAfterXDays.Value, entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrphanedFilesCleaner] Failed to purge orphaned entry: {path}", entry);
            }
        }

        return Task.CompletedTask;
    }
}
