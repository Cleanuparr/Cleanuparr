using System.IO.Enumeration;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <inheritdoc cref="IOrphanedFilesCleanupService" />
public sealed class OrphanedFilesCleanupService : IOrphanedFilesCleanupService
{
    private readonly ILogger<OrphanedFilesCleanupService> _logger;
    private readonly DataContext _dataContext;
    private readonly TimeProvider _timeProvider;
    private readonly IDryRunInterceptor _dryRunInterceptor;

    public OrphanedFilesCleanupService(
        ILogger<OrphanedFilesCleanupService> logger,
        DataContext dataContext,
        TimeProvider timeProvider,
        IDryRunInterceptor dryRunInterceptor)
    {
        _logger = logger;
        _dataContext = dataContext;
        _timeProvider = timeProvider;
        _dryRunInterceptor = dryRunInterceptor;
    }

    public async Task ProcessAsync(IReadOnlyList<IDownloadService> downloadServices, CancellationToken cancellationToken)
    {
        HashSet<Guid> activeClientIds = downloadServices.Select(s => s.ClientConfig.Id).ToHashSet();

        if (activeClientIds.Count is 0)
        {
            _logger.LogWarning("Skipping orphaned-files scan because no download services are available");
            return;
        }

        List<OrphanedFilesConfig> orphanedFilesConfigs = await _dataContext.OrphanedFilesConfigs
            .AsNoTracking()
            .Include(x => x.DownloadClientConfig)
            .Where(x => x.Enabled
                        && x.DownloadClientConfig.Enabled
                        && activeClientIds.Contains(x.DownloadClientConfigId))
            .ToListAsync(cancellationToken);

        if (orphanedFilesConfigs.Count is 0)
        {
            _logger.LogDebug("No orphaned files settings have been configured");
            return;
        }

        List<OrphanedFilesConfig> scannableConfigs = new();
        foreach (OrphanedFilesConfig config in orphanedFilesConfigs)
        {
            if (config.ScanDirectories.Count is 0)
            {
                _logger.LogWarning("skip | no scan directories configured for client {name}", config.DownloadClientConfig.Name);
                continue;
            }

            scannableConfigs.Add(config);
        }

        if (scannableConfigs.Count is 0)
        {
            return;
        }

        HashSet<Guid> clientIdsNeedingScan = scannableConfigs
            .Select(x => x.DownloadClientConfigId)
            .ToHashSet();

        // Build set of content paths claimed by active torrents across clients that have ScanDirectories configured.
        HashSet<string> claimedPaths = new(StringComparer.OrdinalIgnoreCase);
        HashSet<Guid> skippedClientIds = new();

        foreach (IDownloadService downloadService in downloadServices)
        {
            if (!clientIdsNeedingScan.Contains(downloadService.ClientConfig.Id))
            {
                continue;
            }

            bool success = await TryAddClaimedPathsAsync(downloadService, claimedPaths);
            if (!success)
            {
                skippedClientIds.Add(downloadService.ClientConfig.Id);
            }
        }

        _logger.LogDebug("{count} claimed paths across all clients", claimedPaths.Count);

        foreach (OrphanedFilesConfig clientConfig in scannableConfigs)
        {
            if (skippedClientIds.Contains(clientConfig.DownloadClientConfigId))
            {
                _logger.LogWarning("skip | torrents are unavailable or empty | {name}", clientConfig.DownloadClientConfig.Name);
                continue;
            }

            string normalizedOrphanedDir = Path.GetFullPath(clientConfig.OrphanedDirectory)
                .TrimEnd(Path.DirectorySeparatorChar);

            foreach (string scanDir in clientConfig.ScanDirectories)
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
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning {dir} for client {name}", scanDir, clientConfig.DownloadClientConfig.Name);
                }
            }

            PurgeOrphanedDirectory(clientConfig, cancellationToken);
        }
    }

    private async Task<bool> TryAddClaimedPathsAsync(IDownloadService downloadService, HashSet<string> claimedPaths)
    {
        DownloadClientConfig downloadClient = downloadService.ClientConfig;
        List<ITorrentItemWrapper> torrents;
        try
        {
            torrents = await downloadService.GetAllTorrentsLite();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get torrents | {name}", downloadClient.Name);
            return false;
        }

        if (torrents.Count is 0)
        {
            _logger.LogDebug("No torrents found | {name}", downloadClient.Name);
            return false;
        }

        foreach (ITorrentItemWrapper torrent in torrents)
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

        _logger.LogDebug("Loaded {count} torrents | {name}", torrents.Count, downloadClient.Name);
        return true;
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
                    DateTimeOffset lastWrite = File.GetLastWriteTimeUtc(normalizedPath);
                    DateTimeOffset created = File.GetCreationTimeUtc(normalizedPath);
                    DateTimeOffset mostRecent = lastWrite > created ? lastWrite : created;
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

        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (Directory.Exists(path))
        {
            Directory.Move(path, destination);
            Directory.SetLastWriteTimeUtc(destination, now.UtcDateTime);
        }
        else
        {
            File.Move(path, destination);
            File.SetLastWriteTimeUtc(destination, now.UtcDateTime);
        }

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

        DateTimeOffset cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-clientConfig.PurgeAfterHours.Value);

        foreach (string filePath in Directory.EnumerateFileSystemEntries(clientConfig.OrphanedDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            DateTimeOffset lastWrite = File.GetLastWriteTimeUtc(filePath);
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
