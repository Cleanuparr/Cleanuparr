using System.IO.Enumeration;

using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.OrphanedFilesCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

/// <summary>
/// Scans configured directories for orphaned files (files no longer claimed by any
/// torrent in any active download client) and moves them aside. Invoked from
/// <see cref="DownloadCleaner"/> rather than running on its own schedule.
/// </summary>
public sealed class OrphanedFilesCleaner
{
    private readonly ILogger<OrphanedFilesCleaner> _logger;
    private readonly DataContext _dataContext;
    private readonly IDryRunInterceptor _dryRunInterceptor;

    public OrphanedFilesCleaner(
        ILogger<OrphanedFilesCleaner> logger,
        DataContext dataContext,
        IDryRunInterceptor dryRunInterceptor
    )
    {
        _logger = logger;
        _dataContext = dataContext;
        _dryRunInterceptor = dryRunInterceptor;
    }

    /// <summary>
    /// Runs an orphaned-files pass for every download client whose per-client config is enabled.
    /// </summary>
    /// <param name="downloadServices">
    /// Download services that successfully logged in earlier in the run. Used to enumerate
    /// claimed torrent paths across all clients.
    /// </param>
    /// <param name="failedClientIds">
    /// Download client IDs that failed to login. Their scan directories are skipped to avoid
    /// false positives caused by missing claimed paths.
    /// </param>
    public async Task ProcessAsync(
        IReadOnlyList<IDownloadService> downloadServices,
        HashSet<Guid> failedClientIds,
        CancellationToken cancellationToken = default)
    {
        OrphanedFilesCleanerConfig config;
        List<OrphanedFilesClientConfig> clientConfigs;

        await DataContext.Lock.WaitAsync(cancellationToken);
        try
        {
            config = await _dataContext.OrphanedFilesCleanerConfigs.AsNoTracking().FirstAsync(cancellationToken);
            clientConfigs = await _dataContext.OrphanedFilesClientConfigs
                .AsNoTracking()
                .Include(x => x.DownloadClientConfig)
                .Where(x => x.Enabled && x.DownloadClientConfig.Enabled)
                .ToListAsync(cancellationToken);
        }
        finally
        {
            DataContext.Lock.Release();
        }

        if (clientConfigs.Count == 0)
        {
            _logger.LogDebug("No enabled per-client orphaned-files configurations, skipping");
            return;
        }

        // Build set of all content paths claimed by active torrents across ALL download clients
        // (regardless of per-client orphaned config) to avoid false positives across cross-seeded clients.
        var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (IDownloadService downloadService in downloadServices)
        {
            var downloadClient = downloadService.ClientConfig;
            try
            {
                var torrents = await downloadService.GetAllTorrents();

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get torrents from client {name}", downloadClient.Name);
                failedClientIds.Add(downloadClient.Id);
            }
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

            if (failedClientIds.Contains(clientConfig.DownloadClientConfigId))
            {
                _logger.LogWarning("Skipping scan for client {name} — claimed paths could not be loaded, scan would produce false positives",
                    clientConfig.DownloadClientConfig.Name);
                continue;
            }

            if (clientConfig.ScanDirectories.Count == 0)
            {
                _logger.LogWarning("No scan directories configured for client {name}, skipping",
                    clientConfig.DownloadClientConfig.Name);
                continue;
            }

            // Resolve and validate the orphaned directory up front for this client
            string? normalizedOrphanedDir = string.IsNullOrWhiteSpace(clientConfig.OrphanedDirectory)
                ? null
                : Path.GetFullPath(clientConfig.OrphanedDirectory).TrimEnd(Path.DirectorySeparatorChar);

            if (normalizedOrphanedDir is null)
            {
                _logger.LogInformation(
                    "No orphaned directory configured for client {name} — orphaned entries will be logged but not moved",
                    clientConfig.DownloadClientConfig.Name);
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
                    _logger.LogError(ex, "Error scanning {dir} for client {name}", scanDir, clientConfig.DownloadClientConfig.Name);
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

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", new EnumerationOptions { RecurseSubdirectories = false }))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (moved >= remainingSlots)
            {
                break;
            }

            string normalizedEntry = Path.GetFullPath(entry).TrimEnd(Path.DirectorySeparatorChar);

            // Skip reparse points (symlinks/junctions) — moving across link boundaries is unpredictable
            // and following them risks moving files outside the scan tree.
            if ((File.GetAttributes(normalizedEntry) & FileAttributes.ReparsePoint) != 0)
            {
                _logger.LogWarning("skip | reparse point | {path}", normalizedEntry);
                continue;
            }

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
                MoveToOrphanedDirectory(normalizedEntry, normalizedOrphanedDir);
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

            // Reset the timestamp so PurgeOrphanedDirectory ages from the move date, not the original file date
            File.SetLastWriteTimeUtc(capturedDestination, DateTime.UtcNow);

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
