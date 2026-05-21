using System.IO.Enumeration;

using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
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
        List<OrphanedFilesClientConfig> clientConfigs;
        List<DownloadClientConfig> downloadClientConfigs;

        try
        {
            config = await _dataContext.OrphanedFilesCleanerConfigs.AsNoTracking().FirstAsync(cancellationToken);
            clientConfigs = await _dataContext.OrphanedFilesClientConfigs
                .AsNoTracking()
                .Where(x => x.Enabled)
                .ToListAsync(cancellationToken);
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

        if (clientConfigs.Count == 0)
        {
            _logger.LogWarning("No enabled per-client configurations found, skipping");
            return;
        }

        // Build set of all content paths claimed by active torrents across ALL download clients
        // (regardless of per-client orphaned config) to avoid false positives
        var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasAtLeastOneSupportedClient = false;

        // Load ALL per-client configs (including disabled) for path remapping lookup
        await DataContext.Lock.WaitAsync(cancellationToken);
        List<OrphanedFilesClientConfig> allClientConfigs;
        try
        {
            allClientConfigs = await _dataContext.OrphanedFilesClientConfigs
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
        finally
        {
            DataContext.Lock.Release();
        }

        foreach (DownloadClientConfig downloadClient in downloadClientConfigs)
        {
            var perClientConfig = allClientConfigs.FirstOrDefault(c => c.DownloadClientConfigId == downloadClient.Id);

            IDownloadService? downloadService = null;
            try
            {
                downloadService = _downloadServiceFactory.GetDownloadService(downloadClient);
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
                        downloadClient.DownloadDirectorySource,
                        downloadClient.DownloadDirectoryTarget
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
                            downloadClient.DownloadDirectorySource,
                            downloadClient.DownloadDirectoryTarget
                        );

                        claimedPaths.Add(contentPath.TrimEnd(Path.DirectorySeparatorChar));
                    }
                }

                _logger.LogDebug("Loaded {count} torrent paths from {name}", torrents.Count, downloadClient.Name);
            }
            catch (NotSupportedException)
            {
                _logger.LogDebug("Client {name} does not support orphan detection, skipping", downloadClient.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get torrents from client {name}", downloadClient.Name);
            }
            finally
            {
                downloadService?.Dispose();
            }
        }

        if (!hasAtLeastOneSupportedClient)
        {
            _logger.LogWarning("No configured download client supports orphan detection — aborting scan to avoid false positives");
            return;
        }

        _logger.LogDebug("{count} claimed paths across all clients", claimedPaths.Count);

        int processedCount = 0;

        foreach (var clientConfig in clientConfigs)
        {
            if (processedCount >= config.MaxOrphanedFilesToProcess)
            {
                _logger.LogWarning("Reached the limit of {max} orphaned entries per run, stopping scan",
                    config.MaxOrphanedFilesToProcess);
                break;
            }

            if (clientConfig.ScanDirectories.Count == 0)
            {
                _logger.LogWarning("No scan directories configured for client {id}, skipping",
                    clientConfig.DownloadClientConfigId);
                continue;
            }

            // Resolve and validate the orphaned directory up front for this client
            string? normalizedOrphanedDir = string.IsNullOrWhiteSpace(clientConfig.OrphanedDirectory)
                ? null
                : Path.GetFullPath(clientConfig.OrphanedDirectory).TrimEnd(Path.DirectorySeparatorChar);

            if (normalizedOrphanedDir is null)
            {
                _logger.LogInformation(
                    "No orphaned directory configured for client {id} — orphaned entries will be logged but not moved",
                    clientConfig.DownloadClientConfigId);
            }

            foreach (var scanDir in clientConfig.ScanDirectories)
            {
                if (processedCount >= config.MaxOrphanedFilesToProcess)
                {
                    break;
                }

                if (!Directory.Exists(scanDir))
                {
                    _logger.LogWarning("Scan directory does not exist: {dir}", scanDir);
                    continue;
                }

                _logger.LogDebug("Scanning {dir}", scanDir);

                try
                {
                    processedCount += await ScanDirectoryAsync(
                        scanDir, claimedPaths, config, clientConfig, normalizedOrphanedDir,
                        config.MaxOrphanedFilesToProcess - processedCount,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning {dir}", scanDir);
                }
            }

            // Purge old entries from this client's orphaned directory
            PurgeOrphanedDirectory(clientConfig, config, cancellationToken);
        }
    }

    private async Task<int> ScanDirectoryAsync(
        string directory,
        HashSet<string> claimedPaths,
        OrphanedFilesCleanerConfig config,
        OrphanedFilesClientConfig clientConfig,
        string? normalizedOrphanedDir,
        int remainingSlots,
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

            string normalizedEntry = Path.GetFullPath(entry).TrimEnd(Path.DirectorySeparatorChar);

            if (normalizedOrphanedDir is not null &&
                normalizedEntry.Equals(normalizedOrphanedDir, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("skip | orphaned directory itself | {path}", normalizedEntry);
                continue;
            }

            if (claimedPaths.Contains(normalizedEntry))
            {
                _logger.LogDebug("skip | claimed by torrent | {path}", normalizedEntry);
                continue;
            }

            // Exclude by glob pattern
            string entryName = Path.GetFileName(normalizedEntry);
            if (config.ExcludePatterns.Any(pattern =>
                    FileSystemName.MatchesSimpleExpression(pattern, entryName, ignoreCase: true)))
            {
                _logger.LogDebug("skip | excluded by pattern | {path}", normalizedEntry);
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
                        "skip | too recent ({age:F1} min < {min} min) | {path}",
                        ageMinutes, config.MinFileAgeMinutes, normalizedEntry);
                    continue;
                }
            }

            _logger.LogInformation("orphaned entry found | {path}", normalizedEntry);

            if (normalizedOrphanedDir is null)
            {
                continue;
            }

            try
            {
                MoveToOrphanedDirectory(normalizedEntry, clientConfig.OrphanedDirectory!);
                moved++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle orphaned entry: {path}", normalizedEntry);
            }
        }

        return moved;
    }

    private void MoveToOrphanedDirectory(string path, string orphanedDirectory)
    {
        string entryName = Path.GetFileName(path);
        string destination = Path.Combine(orphanedDirectory, entryName);

        if (Path.Exists(destination))
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            destination = Path.Combine(orphanedDirectory, $"{entryName}_{timestamp}");

            int counter = 1;
            while (Path.Exists(destination))
            {
                destination = Path.Combine(orphanedDirectory, $"{entryName}_{timestamp}_{counter}");
                counter++;
            }
        }

        string capturedDestination = destination;

        void DoMove()
        {
            Directory.CreateDirectory(orphanedDirectory);

            if (Directory.Exists(path))
            {
                Directory.Move(path, capturedDestination);
            }
            else
            {
                File.Move(path, capturedDestination);
            }

            _logger.LogInformation("orphaned entry moved | {source} -> {dest}", path, capturedDestination);
        }

        _dryRunInterceptor.Intercept(DoMove);
    }

    private void PurgeOrphanedDirectory(
        OrphanedFilesClientConfig clientConfig,
        OrphanedFilesCleanerConfig config,
        CancellationToken cancellationToken)
    {
        if (!config.EmptyAfterXDays.HasValue)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(clientConfig.OrphanedDirectory) || !Directory.Exists(clientConfig.OrphanedDirectory))
        {
            return;
        }

        DateTime cutoff = DateTime.UtcNow.AddDays(-config.EmptyAfterXDays.Value);

        foreach (var entry in Directory.EnumerateFileSystemEntries(clientConfig.OrphanedDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            DateTime lastWrite = File.GetLastWriteTimeUtc(entry);
            if (lastWrite > cutoff)
            {
                continue;
            }

            try
            {
                int days = config.EmptyAfterXDays.Value;
                string capturedEntry = entry;

                void DoPurge()
                {
                    if (Directory.Exists(capturedEntry))
                    {
                        Directory.Delete(capturedEntry, recursive: true);
                    }
                    else
                    {
                        File.Delete(capturedEntry);
                    }

                    _logger.LogInformation("purged old orphaned entry ({days}d+) | {path}", days, capturedEntry);
                }

                _dryRunInterceptor.Intercept(DoPurge);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge orphaned entry: {path}", entry);
            }
        }
    }
}
