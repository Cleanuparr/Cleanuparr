using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.HealthCheck;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Cleanuparr.Infrastructure.Http;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

/// <summary>
/// Base download client implementation, shared by every protocol.
/// Torrent-only behaviour (seeding, hardlinks, slow/stall rules) lives in <see cref="TorrentDownloadService"/>.
/// </summary>
public abstract class DownloadService : IDownloadService, IQueueCheckCapable, IFileBlockingCapable, IOrphanClaimsCapable
{
    protected readonly ILogger<DownloadService> _logger;
    protected readonly IFilenameEvaluator _filenameEvaluator;
    protected readonly IDryRunInterceptor _dryRunInterceptor;
    protected readonly IEventPublisher _eventPublisher;
    protected readonly IBlocklistProvider _blocklistProvider;
    protected readonly HttpClient _httpClient;
    protected readonly DownloadClientConfig _downloadClientConfig;
    protected readonly TimeProvider _timeProvider;

    protected DownloadService(
        ILogger<DownloadService> logger,
        IFilenameEvaluator filenameEvaluator,
        IDryRunInterceptor dryRunInterceptor,
        IDynamicHttpClientProvider httpClientProvider,
        IEventPublisher eventPublisher,
        IBlocklistProvider blocklistProvider,
        DownloadClientConfig downloadClientConfig,
        TimeProvider timeProvider
    )
    {
        _logger = logger;
        _filenameEvaluator = filenameEvaluator;
        _dryRunInterceptor = dryRunInterceptor;
        _eventPublisher = eventPublisher;
        _blocklistProvider = blocklistProvider;
        _downloadClientConfig = downloadClientConfig;
        _httpClient = httpClientProvider.CreateClient(downloadClientConfig);
        _timeProvider = timeProvider;
    }

    public DownloadClientConfig ClientConfig => _downloadClientConfig;

    protected void SetDownloadClientContext()
    {
        ContextProvider.SetDownloadClient(_downloadClientConfig);
    }

    public abstract void Dispose();

    public abstract Task LoginAsync();

    public abstract Task<HealthCheckResult> HealthCheckAsync();

    /// <inheritdoc cref="IQueueCheckCapable.ShouldRemoveFromArrQueueAsync"/>
    public abstract Task<DownloadCheckResult> ShouldRemoveFromArrQueueAsync(string downloadId, IReadOnlyList<string> ignoredDownloads);

    /// <inheritdoc cref="IOrphanClaimsCapable.GetAllDownloadsLite"/>
    public abstract Task<List<IDownloadItem>> GetAllDownloadsLite();

    /// <inheritdoc cref="IOrphanClaimsCapable.GetClaimedPathsAsync"/>
    public abstract Task<IReadOnlyList<string>> GetClaimedPathsAsync(IReadOnlyList<IDownloadItem> downloads);

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
        IReadOnlyList<IDownloadItem> downloads,
        Func<IDownloadItem, Task<IReadOnlyCollection<string>>> resolveRelativeFilePaths)
    {
        HashSet<string> claimed = new(StringComparer.OrdinalIgnoreCase);

        foreach (IDownloadItem torrent in downloads)
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
    /// The top-level entries a download occupies.
    /// </summary>
    private IReadOnlyList<string> BuildClaimedPaths(IDownloadItem torrent, IReadOnlyCollection<string> relativeFilePaths)
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

    /// <summary>
    /// Scans the files, sets the removal verdict on <paramref name="result"/> and marks the unwanted files as skipped.
    /// </summary>
    /// <param name="markFilesAsSkipped">Marks the given file indices as skipped in the client.</param>
    protected async Task ApplyFileBlockingAsync(
        BlockFilesResult result,
        string name,
        IEnumerable<(int Index, string ValidationName, string LogName, FileBlockAction Action)> files,
        bool deleteIfAnyFileBlocked,
        Func<List<int>, Task> markFilesAsSkipped)
    {
        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);

        (List<int> unwantedIndices, long totalFiles, long totalUnwantedFiles, bool deleteImmediately) =
            ScanFilesForBlocking(files, blocklistType, patterns, regexes, deleteIfAnyFileBlocked);

        if (deleteImmediately)
        {
            _logger.LogDebug("at least one file is blocked for {Name}", name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AtLeastOneFileBlocked;
            return;
        }

        if (unwantedIndices.Count is 0)
        {
            _logger.LogDebug("No unwanted files found for {Name}", name);
            return;
        }

        if (totalUnwantedFiles == totalFiles)
        {
            _logger.LogDebug("All files are blocked for {Name}", name);
            result.ShouldRemove = true;
            result.DeleteReason = DeleteReason.AllFilesBlocked;
        }

        _logger.LogDebug("Marking {Count} unwanted files as skipped for {Name}", unwantedIndices.Count, name);
        await _dryRunInterceptor.InterceptAsync(() => MarkFilesAsSkipped(name, unwantedIndices, markFilesAsSkipped));
    }

    /// <summary>
    /// A failed priority update leaves the verdict in place.
    /// </summary>
    private async Task MarkFilesAsSkipped(string name, List<int> unwantedIndices, Func<List<int>, Task> markFilesAsSkipped)
    {
        try
        {
            await markFilesAsSkipped(unwantedIndices);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to mark files as skipped | {Name}", name);
        }
    }

    /// <inheritdoc cref="IFileBlockingCapable.BlockUnwantedFilesAsync"/>
    public abstract Task<BlockFilesResult> BlockUnwantedFilesAsync(string downloadId, IReadOnlyList<string> ignoredDownloads);

    /// <summary>
    /// Deletes the specified download from the download client.
    /// Each client implementation handles the deletion according to its API requirements.
    /// </summary>
    /// <param name="item">The download item to delete</param>
    /// <param name="deleteSourceFiles">Whether to delete the source files along with the download</param>
    public abstract Task DeleteDownload(IDownloadItem item, bool deleteSourceFiles);

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
