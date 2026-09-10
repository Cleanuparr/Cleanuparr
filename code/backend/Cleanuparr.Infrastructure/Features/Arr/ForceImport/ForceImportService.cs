using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.ManualImport;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr.ForceImport;

public sealed class ForceImportService : IForceImportService
{
    /// <summary>
    /// How long a first sighting of a transitional block is remembered.
    /// </summary>
    private static readonly TimeSpan SightingWindow = TimeSpan.FromHours(6);

    /// <summary>
    /// How long an instance keeps its pending imports once it stops being polled.
    /// </summary>
    private static readonly TimeSpan PendingWindow = TimeSpan.FromHours(6);

    /// <summary>
    /// How long a download that ran out of tries is left to the strike path.
    /// </summary>
    /// <remarks>
    /// Whatever stopped the arr, a full disk or a bad permission, can be fixed by then.
    /// </remarks>
    private static readonly TimeSpan GaveUpWindow = TimeSpan.FromHours(6);

    /// <summary>
    /// The reasons an arr gives that are safe to force past.
    /// </summary>
    /// <remarks>
    /// The arr wrote the files and only its own bookkeeping stopped the import.
    /// </remarks>
    private static readonly string[] SafeReasons =
    [
        "matched to series by ID",
        "matched to movie by ID",
        "Unable to determine if file is a sample",
    ];

    /// <summary>
    /// The commands an arr runs while it moves files.
    /// </summary>
    private static readonly HashSet<string> FileMovingCommands = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "RefreshMonitoredDownloads",
        "ProcessMonitoredDownloads",
        "DownloadedEpisodesScan",
        "DownloadedMoviesScan",
        "ManualImport",
        "RescanSeries",
        "RescanMovie",
        "RenameFiles",
        "RenameSeries",
        "RenameMovie",
    };

    private static readonly HashSet<string> BlockedStates = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "importBlocked",
        "importPending",
        "importFailed",
    };

    private readonly ILogger<ForceImportService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IStriker _striker;
    private readonly IEventPublisher _eventPublisher;

    public ForceImportService(
        ILogger<ForceImportService> logger,
        IMemoryCache cache,
        IStriker striker,
        IEventPublisher eventPublisher
    )
    {
        _logger = logger;
        _cache = cache;
        _striker = striker;
        _eventPublisher = eventPublisher;
    }

    public async Task<ForceImportOutcome> TryImportAsync(IArrClient arrClient, ArrInstance instance, QueueRecord record)
    {
        FailedImportConfig config = ContextProvider.Get<QueueCleanerConfig>().FailedImport;

        if (!config.ForceImport)
        {
            return ForceImportOutcome.NotApplicable;
        }

        if (!arrClient.SupportsForceImport)
        {
            _logger.LogDebug("skip force import | not supported for {type} | {title}", instance.ArrConfig.Type, record.Title);
            return ForceImportOutcome.NotApplicable;
        }

        if (!IsBlockedImport(record) || !EveryReasonIsSafe(record))
        {
            return ForceImportOutcome.NotApplicable;
        }

        if (!arrClient.HasContentId(record))
        {
            // Without a content id there is nothing to match a candidate against.
            _logger.LogDebug("skip force import | item is missing the content id | {title}", record.Title);
            return ForceImportOutcome.NotApplicable;
        }

        string gaveUpKey = CacheKeys.ForceImportGaveUp(record.DownloadId, instance.Url);

        if (_cache.TryGetValue(gaveUpKey, out bool _))
        {
            _logger.LogDebug("skip force import | out of tries | {title}", record.Title);
            return ForceImportOutcome.NotApplicable;
        }

        Dictionary<string, PendingForceImport> pending = GetPending(instance);
        pending.TryGetValue(record.DownloadId, out PendingForceImport? attempt);
        int tries = attempt?.Tries ?? 0;

        if (tries >= config.ForceImportMaxTries)
        {
            // The arr kept the download blocked, so the strike path takes over.
            _cache.Set(gaveUpKey, true, GaveUpWindow);
            pending.Remove(record.DownloadId);

            _logger.LogInformation("give up force import | {tries} tries spent | {title}", tries, record.Title);

            return ForceImportOutcome.NotApplicable;
        }

        if (!WasSeenInAnEarlierRun(instance, record))
        {
            return ForceImportOutcome.Deferred;
        }

        if (await HasImportInFlightAsync(arrClient, instance))
        {
            return ForceImportOutcome.Deferred;
        }

        (List<ManualImportFile>? files, ForceImportOutcome? failure) = await BuildFilesAsync(arrClient, instance, record);

        if (files is null)
        {
            return failure!.Value;
        }

        try
        {
            await arrClient.ForceImportAsync(instance, files);
        }
        catch (Exception exception)
        {
            // One failure must not stop the rest of the queue.
            _logger.LogError(exception, "force import failed | {title}", record.Title);
            return ForceImportOutcome.Deferred;
        }

        pending[record.DownloadId] = new PendingForceImport(record, files.Count, tries + 1);

        _logger.LogInformation(
            "asked the arr to import {count} file(s) | try {try} | {title}",
            files.Count, tries + 1, record.Title
        );

        return ForceImportOutcome.Deferred;
    }

    public async Task ReconcileAsync(ArrInstance instance, IReadOnlySet<string> queuedDownloadIds)
    {
        if (!_cache.TryGetValue(CacheKeys.ForceImportPending(instance.Url), out Dictionary<string, PendingForceImport>? pending) ||
            pending is null)
        {
            return;
        }

        List<string> gone = pending.Keys
            .Where(downloadId => !queuedDownloadIds.Contains(downloadId))
            .ToList();

        foreach (string downloadId in gone)
        {
            PendingForceImport attempt = pending[downloadId];
            pending.Remove(downloadId);

            if (_cache.TryGetValue(CacheKeys.DownloadMarkedForRemoval(downloadId, instance.Url), out bool _))
            {
                // Cleanuparr took the download out of the queue, so nothing was imported.
                continue;
            }

            _logger.LogInformation("force imported {count} file(s) | {title}", attempt.FileCount, attempt.Record.Title);

            // The notification reads the record for its title and its poster.
            ContextProvider.Set(nameof(QueueRecord), attempt.Record);

            await _striker.ResetStrikeAsync(downloadId, attempt.Record.Title, StrikeType.FailedImport);
            await _eventPublisher.PublishForceImported(attempt.Record.Title, downloadId, attempt.FileCount);
        }
    }

    private Dictionary<string, PendingForceImport> GetPending(ArrInstance instance) =>
        _cache.GetOrCreate(CacheKeys.ForceImportPending(instance.Url), entry =>
        {
            entry.SlidingExpiration = PendingWindow;
            return new Dictionary<string, PendingForceImport>(StringComparer.InvariantCultureIgnoreCase);
        })!;

    private static bool IsBlockedImport(QueueRecord record) =>
        record.TrackedDownloadStatus.Equals("warning", StringComparison.InvariantCultureIgnoreCase) &&
        BlockedStates.Contains(record.TrackedDownloadState);

    /// <summary>
    /// One unknown reason means a human has to look at the download.
    /// </summary>
    private bool EveryReasonIsSafe(QueueRecord record)
    {
        List<TrackedDownloadStatusMessage> statuses = record.StatusMessages ?? [];

        if (statuses.Count is 0)
        {
            _logger.LogDebug("skip force import | no status message found | {title}", record.Title);
            return false;
        }

        // The arr states a block on the whole download as a title with nothing under it.
        if (statuses.Any(status => status.Messages?.Any(message => !string.IsNullOrWhiteSpace(message)) is not true))
        {
            _logger.LogDebug("skip force import | a status message carries no reason | {title}", record.Title);
            return false;
        }

        List<string> unsafeMessages = statuses
            .SelectMany(status => status.Messages ?? [])
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Where(message => !IsSafeReason(message))
            .ToList();

        if (unsafeMessages.Count > 0)
        {
            _logger.LogDebug(
                "skip force import | unexpected reason | {reasons} | {title}",
                string.Join("; ", unsafeMessages), record.Title
            );

            return false;
        }

        return true;
    }

    private static bool IsSafeReason(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) &&
        SafeReasons.Any(safe => reason.Contains(safe, StringComparison.InvariantCultureIgnoreCase));

    /// <summary>
    /// The arr's own importer may still pick up a transitional block.
    /// </summary>
    private bool WasSeenInAnEarlierRun(ArrInstance instance, QueueRecord record)
    {
        if (record.TrackedDownloadState.Equals("importBlocked", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        string sightingKey = CacheKeys.ForceImportFirstSeen(record.DownloadId, instance.Url);

        if (_cache.TryGetValue(sightingKey, out bool _))
        {
            return true;
        }

        _cache.Set(sightingKey, true, SightingWindow);
        _logger.LogDebug("skip force import | first sighting | {title}", record.Title);

        return false;
    }

    /// <summary>
    /// A manual import call while the arr moves files reads them mid-move.
    /// </summary>
    private async Task<bool> HasImportInFlightAsync(IArrClient arrClient, ArrInstance instance)
    {
        try
        {
            List<ArrCommandStatus> commands = await arrClient.GetCommandsAsync(instance);

            ArrCommandStatus? running = commands.FirstOrDefault(command =>
                command.Status is ArrCommandState.Queued or ArrCommandState.Started &&
                FileMovingCommands.Contains(command.Name ?? string.Empty));

            if (running is null)
            {
                return false;
            }

            _logger.LogDebug("skip force import | {command} is {status} | {url}", running.Name, running.Status, instance.Url);

            return true;
        }
        catch (Exception exception)
        {
            // Without the command list there is no proof the arr is idle.
            _logger.LogWarning(exception, "skip force import | command list unavailable | {url}", instance.Url);
            return true;
        }
    }

    /// <returns>The files to import, or null and the outcome that leaves the download alone.</returns>
    private async Task<(List<ManualImportFile>? Files, ForceImportOutcome? Failure)> BuildFilesAsync(
        IArrClient arrClient,
        ArrInstance instance,
        QueueRecord record
    )
    {
        List<ManualImportCandidate> candidates;

        try
        {
            candidates = await arrClient.GetManualImportCandidatesAsync(instance, record.DownloadId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "wait for force import | candidates unavailable | {title}", record.Title);
            return (null, ForceImportOutcome.Deferred);
        }

        if (candidates.Count is 0)
        {
            _logger.LogInformation("skip force import | the arr found no file to import | {title}", record.Title);
            return (null, ForceImportOutcome.NotApplicable);
        }

        List<ManualImportFile> files = [];

        foreach (ManualImportCandidate candidate in candidates)
        {
            string name = candidate.RelativePath ?? candidate.Path ?? record.Title;

            List<string> blocking = candidate.Rejections
                ?.Select(rejection => rejection.Reason)
                .Where(reason => !IsSafeReason(reason))
                .Select(reason => reason!)
                .ToList() ?? [];

            if (blocking.Count > 0)
            {
                _logger.LogInformation(
                    "skip force import | unexpected rejection | {reasons} | {name}",
                    string.Join("; ", blocking), name
                );

                return (null, ForceImportOutcome.NotApplicable);
            }

            ManualImportFile? file = arrClient.MapCandidate(record, candidate);

            if (file is null)
            {
                _logger.LogWarning(
                    "skip force import | file maps to other content than the queue item | {name}",
                    name
                );

                return (null, ForceImportOutcome.NotApplicable);
            }

            files.Add(file);
        }

        return (files, null);
    }
}
