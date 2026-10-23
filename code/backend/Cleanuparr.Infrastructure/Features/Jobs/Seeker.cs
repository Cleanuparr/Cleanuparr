using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.Extensions.Hosting;
using Cleanuparr.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class Seeker : IHandler
{
    private const double JitterFactor = 0.2;
    private const int MinJitterSeconds = 30;
    private const int MaxJitterSeconds = 120;

    /// <summary>
    /// Strike types that the Queue Cleaner records against a download.
    /// Seeker leaves a download struck with one of these out of the active download count.
    /// </summary>
    private static readonly StrikeType[] QueueCleanerStrikeTypes =
    [
        StrikeType.Stalled,
        StrikeType.SlowSpeed,
        StrikeType.SlowTime,
        StrikeType.DownloadingMetadata,
        StrikeType.Errored
    ];

    private readonly ILogger<Seeker> _logger;
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IArrQueueReader _arrQueueReader;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;
    private readonly IStatusNotifier _statusNotifier;
    private readonly SeekerCycleLock _cycleLock;
    private readonly SeekerUnitEvaluator _evaluator;

    public Seeker(
        ILogger<Seeker> logger,
        DataContext dataContext,
        EventsContext eventsContext,
        IArrClientFactory arrClientFactory,
        IArrQueueReader arrQueueReader,
        IEventPublisher eventPublisher,
        IDryRunInterceptor dryRunInterceptor,
        IHostEnvironment environment,
        TimeProvider timeProvider,
        IStatusNotifier statusNotifier,
        SeekerCycleLock cycleLock,
        SeekerUnitEvaluator evaluator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _arrClientFactory = arrClientFactory;
        _arrQueueReader = arrQueueReader;
        _eventPublisher = eventPublisher;
        _dryRunInterceptor = dryRunInterceptor;
        _environment = environment;
        _timeProvider = timeProvider;
        _statusNotifier = statusNotifier;
        _cycleLock = cycleLock;
        _evaluator = evaluator;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        SeekerConfig config = await _dataContext.SeekerConfigs
            .AsNoTracking()
            .FirstAsync();

        if (!config.SearchEnabled)
        {
            _logger.LogDebug("Search is disabled");
            return;
        }

        await ApplyJitter(config, cancellationToken);

        bool isDryRun = await _dryRunInterceptor.IsDryRunEnabled();

        // Replacement searches queued after download removal
        SearchQueueItem? replacementItem = await _eventsContext.SearchQueue
            .OrderBy(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        if (replacementItem is not null)
        {
            await ProcessReplacementItemAsync(replacementItem, isDryRun);
            await _statusNotifier.NotifySearchStatsUpdatedAsync(cancellationToken);
            return;
        }

        // Missing items and quality upgrades
        if (!config.ProactiveSearchEnabled)
        {
            return;
        }

        await ProcessProactiveSearchAsync(config, isDryRun, cancellationToken);

        await _statusNotifier.NotifySearchStatsUpdatedAsync(cancellationToken);
    }

    private async Task ApplyJitter(SeekerConfig config, CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment())
        {
            return;
        }

        int proportionalJitter = (int)(config.SearchInterval * 60 * JitterFactor);
        int maxJitterSeconds = Math.Clamp(proportionalJitter, MinJitterSeconds, MaxJitterSeconds);
        int jitterSeconds = Random.Shared.Next(0, maxJitterSeconds + 1);

        if (jitterSeconds > 0)
        {
            _logger.LogDebug("Waiting {Jitter}s before searching", jitterSeconds);
            await Task.Delay(TimeSpan.FromSeconds(jitterSeconds), _timeProvider, cancellationToken);
        }
    }

    private async Task ProcessReplacementItemAsync(SearchQueueItem item, bool isDryRun)
    {
        ArrInstance? arrInstance = await _dataContext.ArrInstances
            .Include(a => a.ArrConfig)
            .FirstOrDefaultAsync(a => a.Id == item.ArrInstanceId);

        if (arrInstance is null)
        {
            _logger.LogWarning(
                "Skipping replacement search for '{Title}' — arr instance {InstanceId} no longer exists",
                item.Title, item.ArrInstanceId);
            _eventsContext.SearchQueue.Remove(item);
            await _eventsContext.SaveChangesAsync();
            return;
        }

        ContextProvider.Set(nameof(InstanceType), arrInstance.ArrConfig.Type);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceId, arrInstance.Id);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, arrInstance.ExternalOrInternalUrl);

        try
        {
            IArrClient arrClient = _arrClientFactory.GetClient(arrInstance.ArrConfig.Type, arrInstance.Version);
            SearchItem searchItem = BuildSearchItem(item);

            long commandId = await arrClient.SearchItemAsync(arrInstance, searchItem);

            Guid eventId = await _eventPublisher.PublishSearchTriggered(
                item.Title, SeekerSearchType.Replacement, SeekerSearchReason.Replacement, isDryRun: isDryRun);

            if (!isDryRun)
            {
                (long externalItemId, long episodeId, int seasonNumber) = ResolveTrackedIds(searchItem);

                await SaveCommandTrackerAsync(commandId, eventId, arrInstance.Id, arrInstance.ArrConfig.Type,
                    externalItemId, item.Title, seasonNumber, episodeId);
            }

            _logger.LogInformation("Replacement search triggered for '{Title}' on {InstanceName}",
                item.Title, arrInstance.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process replacement search for '{Title}' on {InstanceName}",
                item.Title, arrInstance.Name);
        }
        finally
        {
            if (!isDryRun)
            {
                _eventsContext.SearchQueue.Remove(item);
                await _eventsContext.SaveChangesAsync();
            }
        }
    }

    private static SearchItem BuildSearchItem(SearchQueueItem item)
    {
        if (item.SeriesId.HasValue && Enum.TryParse<SeriesSearchType>(item.SearchType, out var searchType))
        {
            return new SeriesSearchItem
            {
                Id = item.ItemId,
                SeriesId = item.SeriesId.Value,
                SearchType = searchType
            };
        }

        return new SearchItem { Id = item.ItemId };
    }

    /// <summary>
    /// Splits a search item into the ids the SeekerCommandMonitor matches queue records against.
    /// An episode search targets the episode, every other search targets its library item.
    /// </summary>
    private static (long ExternalItemId, long EpisodeId, int SeasonNumber) ResolveTrackedIds(SearchItem searchItem) =>
        searchItem switch
        {
            SeriesSearchItem { SearchType: SeriesSearchType.Episode } series => (series.SeriesId, series.Id, 0),
            SeriesSearchItem { SearchType: SeriesSearchType.Season } series => (series.SeriesId, 0, (int)series.Id),
            _ => (searchItem.Id, 0, 0),
        };

    private async Task ProcessProactiveSearchAsync(SeekerConfig config, bool isDryRun, CancellationToken cancellationToken)
    {
        List<SeekerInstanceConfig> instanceConfigs = await _dataContext.SeekerInstanceConfigs
            .Include(s => s.ArrInstance.ArrConfig)
            .Where(s => s.Enabled && s.ArrInstance.Enabled)
            .ToListAsync();

        instanceConfigs = instanceConfigs
            .Where(s => s.ArrInstance.ArrConfig.Type is InstanceType.Sonarr or InstanceType.Radarr)
            .ToList();

        if (instanceConfigs.Count == 0)
        {
            _logger.LogDebug("No enabled Seeker instances found for proactive search");
            return;
        }

        if (config.UseRoundRobin)
        {
            // Round-robin: try instances in order of oldest LastProcessedAt,
            // stop after the first one that triggers a search.
            // This prevents cycle-complete-waiting instances from wasting a run.
            var ordered = instanceConfigs
                .OrderBy(s => s.LastProcessedAt ?? DateTimeOffset.MinValue)
                .ToList();

            foreach (SeekerInstanceConfig instance in ordered)
            {
                bool searched = await ProcessSingleInstanceAsync(config, instance, isDryRun, cancellationToken);

                if (searched)
                {
                    break;
                }
            }
        }
        else
        {
            // Process all enabled instances sequentially
            foreach (SeekerInstanceConfig instanceConfig in instanceConfigs)
            {
                await ProcessSingleInstanceAsync(config, instanceConfig, isDryRun, cancellationToken);
            }
        }
    }

    private async Task<bool> ProcessSingleInstanceAsync(SeekerConfig config, SeekerInstanceConfig instanceConfig, bool isDryRun, CancellationToken cancellationToken)
    {
        Guid arrInstanceId = instanceConfig.ArrInstanceId;
        await _cycleLock.WaitAsync(arrInstanceId, cancellationToken);

        try
        {
            // A cycle reset may have changed CurrentCycleId while this run waited for the lock
            await _dataContext.Entry(instanceConfig).ReloadAsync(cancellationToken);
            return await ProcessLockedInstanceAsync(config, instanceConfig, isDryRun, cancellationToken);
        }
        finally
        {
            _cycleLock.Release(arrInstanceId);
        }
    }

    private async Task<bool> ProcessLockedInstanceAsync(SeekerConfig config, SeekerInstanceConfig instanceConfig, bool isDryRun, CancellationToken cancellationToken)
    {
        ArrInstance arrInstance = instanceConfig.ArrInstance;
        InstanceType instanceType = arrInstance.ArrConfig.Type;

        _logger.LogDebug("Processing {InstanceType} instance: {InstanceName}",
            instanceType, arrInstance.Name);

        // Set context for event publishing
        ContextProvider.Set(nameof(InstanceType), instanceType);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceId, arrInstance.Id);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, arrInstance.ExternalOrInternalUrl);

        // Fetch queue once for both active download limit check and queue cross-referencing
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType, arrInstance.Version);
        List<QueueRecord> queueRecords;

        try
        {
            queueRecords = await _arrQueueReader.ReadAllAsync(arrClient, arrInstance);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping proactive search for {InstanceName}, failed to read queue",
                arrInstance.Name);
            return false;
        }

        // Check active download limit using the fetched queue data
        if (instanceConfig.ActiveDownloadLimit > 0)
        {
            List<string> downloadIds = queueRecords
                .Where(r => r.SizeLeft > 0 && !string.IsNullOrEmpty(r.DownloadId))
                .Select(r => r.DownloadId.ToLowerInvariant())
                .Distinct()
                .ToList();

            int activeDownloads = instanceConfig.IgnoreStruckDownloads
                ? await DiscountStruckDownloadsAsync(downloadIds, arrInstance.Name)
                : downloadIds.Count;

            if (activeDownloads >= instanceConfig.ActiveDownloadLimit)
            {
                _logger.LogInformation(
                    "Skipping proactive search for {InstanceName} — {Count} items actively downloading (limit: {Limit})",
                    arrInstance.Name, activeDownloads, instanceConfig.ActiveDownloadLimit);
                return false;
            }
        }

        SeekerRecentGrabs recentGrabs;
        try
        {
            recentGrabs = await _evaluator.ReadRecentGrabsAsync(instanceConfig, arrInstance, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping proactive search for {InstanceName}, failed to read grab history",
                arrInstance.Name);
            return false;
        }

        bool searched = false;
        try
        {
            searched = await ProcessInstanceAsync(config, instanceConfig, arrInstance, instanceType, isDryRun, queueRecords,
                recentGrabs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {InstanceType} instance: {InstanceName}",
                instanceType, arrInstance.Name);
        }

        // Update LastProcessedAt so round-robin moves on
        instanceConfig.LastProcessedAt = _timeProvider.GetUtcNow();
        await _dataContext.SaveChangesAsync();

        return searched;
    }

    /// <summary>
    /// Counts the queued downloads the Queue Cleaner left alone on its last completed run.
    /// </summary>
    private async Task<int> DiscountStruckDownloadsAsync(List<string> downloadIds, string instanceName)
    {
        // A run still in flight or a failed one has only struck part of the queue.
        Guid lastRunId = await _eventsContext.JobRuns
            .AsNoTracking()
            .Where(j => j.Type == JobType.QueueCleaner && j.Status == JobRunStatus.Completed)
            .OrderByDescending(j => j.StartedAt)
            .Select(j => j.Id)
            .FirstOrDefaultAsync();

        if (lastRunId == Guid.Empty)
        {
            _logger.LogDebug(
                "No completed Queue Cleaner run to read strikes from, counting every download for {InstanceName}",
                instanceName);

            return downloadIds.Count;
        }

        int struckCount = await _eventsContext.DownloadItems
            .AsNoTracking()
            .Where(d => downloadIds.Contains(d.DownloadId))
            .Where(d => d.IsMarkedForRemoval ||
                        d.Strikes.Any(s => s.JobRunId == lastRunId && QueueCleanerStrikeTypes.Contains(s.Type)))
            .Select(d => d.DownloadId)
            .Distinct()
            .CountAsync();

        if (struckCount > 0)
        {
            _logger.LogDebug(
                "Discounting {Count} struck downloads from the active download count for {InstanceName}",
                struckCount, instanceName);
        }

        return downloadIds.Count - struckCount;
    }

    private async Task<bool> ProcessInstanceAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        InstanceType instanceType,
        bool isDryRun,
        List<QueueRecord> queueRecords,
        SeekerRecentGrabs recentGrabs,
        CancellationToken cancellationToken)
    {
        SeekerCycle cycle = await GetCurrentCycleAsync(arrInstance.Id, instanceConfig, isDryRun, cancellationToken);

        List<SeekerHistory> currentCycleHistory = await _eventsContext.SeekerHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == arrInstance.Id
                        && h.CycleId == cycle.Id
                        && h.IsDryRun == isDryRun)
            .ToListAsync(cancellationToken);

        // Load all history for stale cleanup
        List<long> allHistoryExternalIds = await _eventsContext.SeekerHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == arrInstance.Id)
            .Select(x => x.ExternalItemId)
            .ToListAsync();

        SeekerEvaluation evaluation = await _evaluator.EvaluateAsync(
            config, instanceConfig, arrInstance, queueRecords, recentGrabs, currentCycleHistory, cancellationToken);

        SeekerProcessResult result = await SelectCandidatesAsync(config, instanceConfig, arrInstance, evaluation, cycle, isDryRun);

        if (!result.Evaluation.WantedUnsupported)
        {
            RecordFunnel(result.Cycle, result.Evaluation);
        }

        await _eventsContext.SaveChangesAsync(cancellationToken);

        if (result.Candidates.Count == 0)
        {
            _logger.LogDebug("No items selected for search on {InstanceName}", arrInstance.Name);
            if (!isDryRun)
            {
                await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, result.Evaluation.AllLibraryIds, allHistoryExternalIds);
            }
            return false;
        }

        // Search each item individually so each gets its own event and command tracker
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType, arrInstance.Version);

        foreach (SeekerSearchCandidate candidate in result.Candidates)
        {
            SearchItem searchItem = instanceType switch
            {
                InstanceType.Radarr => new SearchItem { Id = candidate.ItemId },
                _ when candidate.EpisodeId > 0 => new SeriesSearchItem
                {
                    Id = candidate.EpisodeId,
                    SeriesId = candidate.ItemId,
                    SearchType = SeriesSearchType.Episode
                },
                _ => new SeriesSearchItem
                {
                    Id = candidate.SeasonNumber,
                    SeriesId = candidate.ItemId,
                    SearchType = SeriesSearchType.Season
                },
            };

            long commandId = await arrClient.SearchItemAsync(arrInstance, searchItem);

            Guid eventId = await _eventPublisher.PublishSearchTriggered(
                candidate.Name, SeekerSearchType.Proactive, candidate.Reason, result.Cycle.Id, isDryRun, candidate.ContentKind);

            _logger.LogInformation("Search triggered for {Item} ({Reason}) | {InstanceUrl}", candidate.Name, candidate.Reason, arrInstance.Url);

            if (result.Cycle.SearchedUnits == 0)
            {
                result.Cycle.StartedAt = _timeProvider.GetUtcNow();
            }

            result.Cycle.SearchedUnits++;
            result.Cycle.RemainingUnits = Math.Max(0, result.Cycle.RemainingUnits - 1);
            result.Cycle.CompletedAt = null;
            result.Cycle.CompletionReason = null;

            await UpdateSearchHistoryAsync(arrInstance.Id, instanceType, result.Cycle.Id, candidate, isDryRun);

            if (!isDryRun)
            {
                await SaveCommandTrackerAsync(commandId, eventId, arrInstance.Id, instanceType,
                    candidate.ItemId, candidate.Name, candidate.SeasonNumber, candidate.EpisodeId);
            }
        }

        if (!isDryRun)
        {
            await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, result.Evaluation.AllLibraryIds, allHistoryExternalIds);
        }

        return true;
    }

    /// <summary>
    /// Updates the cycle from the evaluation, rolls it over once nothing remains, and picks the next unit to search.
    /// </summary>
    private async Task<SeekerProcessResult> SelectCandidatesAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        SeekerEvaluation evaluation,
        SeekerCycle cycle,
        bool isDryRun)
    {
        if (evaluation.WantedUnsupported)
        {
            return new SeekerProcessResult { Candidates = [], Cycle = cycle, Evaluation = evaluation };
        }

        int eligibleCount = evaluation.Units.Count;
        cycle.EligibleNow = eligibleCount;
        cycle.RemainingUnits = 0;

        int blockedCount = evaluation.Units.Count(u => u.BlockedBy is not null);
        if (blockedCount > 0)
        {
            _logger.LogDebug("Excluded {Count} units already in queue on {InstanceName}",
                blockedCount, arrInstance.Name);
        }

        if (blockedCount == eligibleCount)
        {
            return await CompleteEmptyCycleAsync(cycle, instanceConfig, isDryRun, arrInstance.Name, evaluation);
        }

        cycle.RemainingUnits = evaluation.Remaining.Count();

        if (cycle.RemainingUnits == 0)
        {
            SeekerCycle? next = await CompleteCycleAsync(cycle, instanceConfig, isDryRun, eligibleCount, evaluation.RestingUnits, arrInstance.Name);
            if (next is null)
            {
                return new SeekerProcessResult { Candidates = [], Cycle = cycle, Evaluation = evaluation };
            }

            cycle = next;
            evaluation = evaluation.WithoutCycleHistory();
            cycle.EligibleNow = evaluation.Units.Count;
            cycle.RemainingUnits = evaluation.Remaining.Count();

            if (cycle.RemainingUnits == 0)
            {
                return new SeekerProcessResult { Candidates = [], Cycle = cycle, Evaluation = evaluation };
            }
        }

        List<SeekerSearchCandidate> candidates = SeekerUnitEvaluator.SelectNext(
            evaluation, SeekerUnitEvaluator.EffectiveStrategy(config.SelectionStrategy, instanceConfig), 1);

        foreach (SeekerSearchCandidate candidate in candidates)
        {
            _logger.LogDebug("Selected '{Name}' ({ContentKind}) for search on {InstanceName}: {Reason}",
                candidate.Name, candidate.ContentKind, arrInstance.Name, candidate.Reason);
        }

        return new SeekerProcessResult { Candidates = candidates, Cycle = cycle, Evaluation = evaluation };
    }

    private void RecordFunnel(SeekerCycle cycle, SeekerEvaluation evaluation)
    {
        cycle.LibraryItems = evaluation.AllLibraryIds.Count;
        cycle.ReleasedItems = evaluation.ReleasedItems;
        cycle.MonitoredItems = evaluation.MonitoredItems;
        cycle.TagFilteredItems = evaluation.TagFilteredItems;
        cycle.QueuedUnits = evaluation.QueuedUnits;
        cycle.RecentlyGrabbedUnits = evaluation.RecentlyGrabbedUnits;
        cycle.RestingUnits = evaluation.RestingUnits;
        cycle.FunnelCountedAt = _timeProvider.GetUtcNow();
    }

    private async Task UpdateSearchHistoryAsync(
        Guid arrInstanceId,
        InstanceType instanceType,
        Guid cycleId,
        SeekerSearchCandidate candidate,
        bool isDryRun)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        SeekerContentKind contentKind = candidate.ContentKind ?? SeekerContentKind.Standard;

        SeekerHistory? existing = await _eventsContext.SeekerHistory
            .FirstOrDefaultAsync(h =>
                h.ArrInstanceId == arrInstanceId
                && h.ExternalItemId == candidate.ItemId
                && h.ItemType == instanceType
                && h.SeasonNumber == candidate.SeasonNumber
                && h.EpisodeId == candidate.EpisodeId
                && h.IsDryRun == isDryRun);

        if (existing is not null)
        {
            existing.CycleId = cycleId;
            existing.LastSearchedAt = now;
            existing.SearchCount++;
            existing.LastOutcome = null;
            existing.ItemTitle = candidate.Name;
            existing.ContentKind = contentKind;
        }
        else
        {
            _eventsContext.SeekerHistory.Add(new SeekerHistory
            {
                ArrInstanceId = arrInstanceId,
                ExternalItemId = candidate.ItemId,
                ItemType = instanceType,
                SeasonNumber = candidate.SeasonNumber,
                EpisodeId = candidate.EpisodeId,
                ContentKind = contentKind,
                CycleId = cycleId,
                LastSearchedAt = now,
                ItemTitle = candidate.Name,
                IsDryRun = isDryRun,
            });
        }

        await _eventsContext.SaveChangesAsync();
    }

    private async Task SaveCommandTrackerAsync(
        long commandId,
        Guid eventId,
        Guid arrInstanceId,
        InstanceType instanceType,
        long externalItemId,
        string itemTitle,
        int seasonNumber = 0,
        long episodeId = 0)
    {
        _eventsContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
        {
            ArrInstanceId = arrInstanceId,
            CommandId = commandId,
            EventId = eventId,
            ExternalItemId = externalItemId,
            EpisodeId = episodeId,
            ItemTitle = itemTitle,
            SeasonNumber = seasonNumber,
            CreatedAt = _timeProvider.GetUtcNow(),
        });

        await _eventsContext.SaveChangesAsync();
    }

    private async Task CleanupStaleHistoryAsync(
        Guid arrInstanceId,
        InstanceType instanceType,
        List<long> currentLibraryIds,
        IEnumerable<long> historyExternalIds)
    {
        // Find history entries for items no longer in the library
        var staleIds = historyExternalIds
            .Except(currentLibraryIds)
            .Distinct()
            .ToList();

        if (staleIds.Count == 0)
        {
            return;
        }

        await _eventsContext.SeekerHistory
            .Where(h => h.ArrInstanceId == arrInstanceId
                && h.ItemType == instanceType
                && staleIds.Contains(h.ExternalItemId))
            .ExecuteDeleteAsync();

        _logger.LogDebug(
            "Cleaned up {Count} stale Seeker history entries for instance {InstanceId}",
            staleIds.Count,
            arrInstanceId
        );
    }

    /// <summary>
    /// Loads the cycle the instance is in, creating its row when missing.
    /// Dry run follows its own newest cycle and never touches the live one.
    /// </summary>
    private async Task<SeekerCycle> GetCurrentCycleAsync(
        Guid arrInstanceId,
        SeekerInstanceConfig instanceConfig,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        if (isDryRun)
        {
            SeekerCycle? dryCycle = await _eventsContext.SeekerCycles
                .Where(c => c.ArrInstanceId == arrInstanceId && c.IsDryRun)
                .OrderByDescending(c => c.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return dryCycle ?? OpenCycle(arrInstanceId, isDryRun: true, eligibleAtStart: 0);
        }

        Guid currentCycleId = instanceConfig.CurrentCycleId;
        List<SeekerCycle> openOrCurrent = await _eventsContext.SeekerCycles
            .Where(c => c.ArrInstanceId == arrInstanceId
                        && (c.Id == currentCycleId || (!c.IsDryRun && c.CompletedAt == null)))
            .ToListAsync(cancellationToken);

        // The migration guesses the open cycle from search events, the config holds the real one.
        foreach (SeekerCycle stale in openOrCurrent.Where(c => c.Id != currentCycleId))
        {
            stale.CompletedAt = await _eventsContext.Events
                .Where(e => e.CycleId == stale.Id)
                .OrderByDescending(e => e.Timestamp)
                .Select(e => (DateTimeOffset?)e.Timestamp)
                .FirstOrDefaultAsync(cancellationToken) ?? stale.StartedAt;
            stale.CompletionReason = SeekerCycleCompletionReason.Completed;
        }

        SeekerCycle? cycle = openOrCurrent.FirstOrDefault(c => c.Id == currentCycleId);
        if (cycle is not null)
        {
            if (!cycle.IsDryRun)
            {
                return cycle;
            }

            // The all-dry-events migration can hand this id to a dry cycle; a live run reclaims it instead of colliding with it.
            // Upgraded live history rows can already carry this id; reclaiming would zero their count.
            SeekerCycle reclaimed = OpenCycle(arrInstanceId, isDryRun: false, eligibleAtStart: 0, reclaim: cycle);
            await RebuildSearchedUnitsFromHistoryAsync(reclaimed, arrInstanceId, currentCycleId, cancellationToken);
            return reclaimed;
        }

        cycle = OpenCycle(arrInstanceId, isDryRun: false, eligibleAtStart: 0, currentCycleId);
        await RebuildSearchedUnitsFromHistoryAsync(cycle, arrInstanceId, currentCycleId, cancellationToken);
        return cycle;
    }

    /// <summary>
    /// Restores SearchedUnits and StartedAt from live SeekerHistory rows already tagged with this cycle id.
    /// </summary>
    private async Task RebuildSearchedUnitsFromHistoryAsync(
        SeekerCycle cycle,
        Guid arrInstanceId,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        List<DateTimeOffset> searchedAt = await _eventsContext.SeekerHistory
            .Where(h => h.ArrInstanceId == arrInstanceId && h.CycleId == cycleId && !h.IsDryRun)
            .Select(h => h.LastSearchedAt)
            .ToListAsync(cancellationToken);

        cycle.SearchedUnits = searchedAt.Count;
        if (searchedAt.Count > 0)
        {
            cycle.StartedAt = searchedAt.Min();
        }
    }

    private SeekerCycle OpenCycle(Guid arrInstanceId, bool isDryRun, int eligibleAtStart, Guid? id = null, SeekerCycle? reclaim = null)
    {
        if (reclaim is not null)
        {
            reclaim.StartedAt = _timeProvider.GetUtcNow();
            reclaim.EligibleAtStart = eligibleAtStart;
            reclaim.EligibleNow = eligibleAtStart;
            reclaim.RemainingUnits = 0;
            reclaim.SearchedUnits = 0;
            reclaim.GrabbedUnits = 0;
            reclaim.CompletedAt = null;
            reclaim.CompletionReason = null;
            reclaim.IsDryRun = isDryRun;
            return reclaim;
        }

        SeekerCycle cycle = new()
        {
            Id = id ?? Guid.NewGuid(),
            ArrInstanceId = arrInstanceId,
            StartedAt = _timeProvider.GetUtcNow(),
            EligibleAtStart = eligibleAtStart,
            EligibleNow = eligibleAtStart,
            IsDryRun = isDryRun,
        };

        _eventsContext.SeekerCycles.Add(cycle);
        return cycle;
    }

    /// <summary>
    /// Closes a cycle with nothing left to search and opens the next one once the minimum cycle time has passed.
    /// Returns the new cycle, or null while the instance has to wait.
    /// </summary>
    private async Task<SeekerCycle?> CompleteCycleAsync(
        SeekerCycle cycle,
        SeekerInstanceConfig instanceConfig,
        bool isDryRun,
        int eligibleCount,
        int restingUnits,
        string instanceName)
    {
        // An empty cycle with nothing resting stays open, so an idle instance does not roll a cycle every week.
        if (cycle.SearchedUnits == 0 && restingUnits == 0)
        {
            return null;
        }

        if (cycle.CompletedAt is null)
        {
            cycle.CompletedAt = _timeProvider.GetUtcNow();
            cycle.CompletionReason = SeekerCycleCompletionReason.Completed;

            if (cycle.SearchedUnits > 0)
            {
                _logger.LogInformation("Cycle completed on {InstanceName} after {Count} searches",
                    instanceName, cycle.SearchedUnits);

                await _eventPublisher.PublishSeekerCycleCompleted(cycle, instanceName);
            }
        }

        if (ShouldWaitForMinCycleTime(instanceConfig, cycle.StartedAt))
        {
            _logger.LogDebug(
                "skip | cycle complete but min time ({Days}) not elapsed (started {StartedAt}) | {InstanceName}",
                instanceConfig.MinCycleTimeDays, cycle.StartedAt, instanceName);
            return null;
        }

        SeekerCycle next = OpenCycle(cycle.ArrInstanceId, isDryRun, eligibleCount);

        if (!isDryRun)
        {
            instanceConfig.CurrentCycleId = next.Id;
            await _dataContext.SaveChangesAsync();
        }

        _logger.LogInformation("Starting new cycle on {InstanceName} with {Count} eligible items",
            instanceName, eligibleCount);

        return next;
    }

    /// <summary>
    /// Early-exit helper for a run with no candidates to search: still completes the cycle when it already
    /// searched something or has resting units, so a cycle with nothing left does not stay "in progress" forever.
    /// </summary>
    private async Task<SeekerProcessResult> CompleteEmptyCycleAsync(
        SeekerCycle cycle,
        SeekerInstanceConfig instanceConfig,
        bool isDryRun,
        string instanceName,
        SeekerEvaluation evaluation)
    {
        SeekerCycle? next = await CompleteCycleAsync(cycle, instanceConfig, isDryRun, evaluation.Units.Count, evaluation.RestingUnits, instanceName);
        return new SeekerProcessResult { Candidates = [], Cycle = next ?? cycle, Evaluation = evaluation };
    }

    /// <summary>
    /// Checks whether the minimum cycle time constraint prevents starting a new cycle.
    /// Returns true if the cycle started recently and MinCycleTimeDays has not yet elapsed.
    /// </summary>
    private bool ShouldWaitForMinCycleTime(SeekerInstanceConfig instanceConfig, DateTimeOffset cycleStartedAt)
    {
        TimeSpan elapsed = _timeProvider.GetUtcNow() - cycleStartedAt;
        return elapsed.TotalDays < instanceConfig.MinCycleTimeDays;
    }
}
