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
using System.Net;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class Seeker : IHandler
{
    private const double JitterFactor = 0.2;
    private const int MinJitterSeconds = 30;
    private const int MaxJitterSeconds = 120;

    /// <summary>
    /// Queue states that indicate an item is actively being processed.
    /// Items in these states are excluded from proactive searches.
    /// "importFailed" is intentionally excluded — failed imports should be re-searched.
    /// </summary>
    private static readonly HashSet<string> ActiveQueueStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "downloading",
        "importing",
        "importPending",
        "importBlocked"
    };

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
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IArrQueueReader _arrQueueReader;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;
    private readonly IStatusNotifier _statusNotifier;

    public Seeker(
        ILogger<Seeker> logger,
        DataContext dataContext,
        EventsContext eventsContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        IArrClientFactory arrClientFactory,
        IArrQueueReader arrQueueReader,
        IEventPublisher eventPublisher,
        IDryRunInterceptor dryRunInterceptor,
        IHostEnvironment environment,
        TimeProvider timeProvider,
        IStatusNotifier statusNotifier)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _arrClientFactory = arrClientFactory;
        _arrQueueReader = arrQueueReader;
        _eventPublisher = eventPublisher;
        _dryRunInterceptor = dryRunInterceptor;
        _environment = environment;
        _timeProvider = timeProvider;
        _statusNotifier = statusNotifier;
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

        HashSet<long> recentlyGrabbedMovieIds = [];
        HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)> recentlyGrabbedEpisodes = [];

        if (instanceConfig.RecentGrabWindowHours > 0)
        {
            DateTimeOffset since = _timeProvider.GetUtcNow().AddHours(-instanceConfig.RecentGrabWindowHours);

            try
            {
                if (instanceType == InstanceType.Radarr)
                {
                    recentlyGrabbedMovieIds = await _radarrClient.GetRecentlyGrabbedMovieIdsAsync(arrInstance, since, cancellationToken);
                }
                else
                {
                    recentlyGrabbedEpisodes = await _sonarrClient.GetRecentlyGrabbedEpisodesAsync(arrInstance, since, cancellationToken);
                }
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
        }

        bool searched = false;
        try
        {
            searched = await ProcessInstanceAsync(config, instanceConfig, arrInstance, instanceType, isDryRun, queueRecords,
                recentlyGrabbedMovieIds, recentlyGrabbedEpisodes, cancellationToken);
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
        HashSet<long> recentlyGrabbedMovieIds,
        HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)> recentlyGrabbedEpisodes,
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

        // Derive item-level history for selection strategies
        Dictionary<long, DateTimeOffset> itemSearchHistory = currentCycleHistory
            .GroupBy(h => h.ExternalItemId)
            .ToDictionary(g => g.Key, g => g.Max(h => h.LastSearchedAt));

        // Build queued-item lookups from active queue records
        var activeQueueRecords = queueRecords
            .Where(r => ActiveQueueStates.Contains(r.TrackedDownloadState))
            .ToList();

        SeekerProcessResult result;

        if (instanceType == InstanceType.Radarr)
        {
            HashSet<long> queuedMovieIds = activeQueueRecords
                .Where(r => r.MovieId > 0)
                .Select(r => r.MovieId)
                .ToHashSet();
            queuedMovieIds.UnionWith(recentlyGrabbedMovieIds);

            result = await ProcessRadarrAsync(config, arrInstance, instanceConfig, itemSearchHistory, cycle, isDryRun, queuedMovieIds, cancellationToken);
        }
        else
        {
            HashSet<(long SeriesId, long SeasonNumber)> queuedSeasons = activeQueueRecords
                .Where(r => r.SeriesId > 0)
                .Select(r => (r.SeriesId, r.SeasonNumber))
                .ToHashSet();
            queuedSeasons.UnionWith(recentlyGrabbedEpisodes.Select(e => (e.SeriesId, e.SeasonNumber)));

            HashSet<long> queuedEpisodeIds = activeQueueRecords
                .Where(r => r.EpisodeId > 0)
                .Select(r => r.EpisodeId)
                .ToHashSet();
            queuedEpisodeIds.UnionWith(recentlyGrabbedEpisodes.Select(e => e.EpisodeId));

            result = await ProcessSonarrAsync(config, arrInstance, instanceConfig, itemSearchHistory, currentCycleHistory, cycle, isDryRun, queuedSeasons, queuedEpisodeIds, cancellationToken);
        }

        await _eventsContext.SaveChangesAsync(cancellationToken);

        if (result.Candidates.Count == 0)
        {
            _logger.LogDebug("No items selected for search on {InstanceName}", arrInstance.Name);
            if (!isDryRun)
            {
                await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, result.AllLibraryIds, allHistoryExternalIds);
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
            await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, result.AllLibraryIds, allHistoryExternalIds);
        }

        return true;
    }

    private async Task<SeekerProcessResult> ProcessRadarrAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        SeekerInstanceConfig instanceConfig,
        Dictionary<long, DateTimeOffset> searchHistory,
        SeekerCycle cycle,
        bool isDryRun,
        HashSet<long> queuedMovieIds,
        CancellationToken cancellationToken)
    {
        List<Tag> tags = await _radarrClient.GetAllTagsAsync(arrInstance);
        Dictionary<long, string> tagsById = tags.ToDictionary(t => t.Id, t => t.Label);
        HashSet<string> skipTagSet = new(instanceConfig.SkipTags, StringComparer.InvariantCultureIgnoreCase);
        HashSet<string> includeTagSet = new(instanceConfig.IncludeTags, StringComparer.InvariantCultureIgnoreCase);

        // Load cached CF scores when custom format score filtering is enabled
        Dictionary<long, CustomFormatScoreEntry>? cfScores = null;
        HashSet<int> upgradesDisabledProfileIds = [];
        if (instanceConfig.UseCustomFormatScore)
        {
            cfScores = await _eventsContext.CustomFormatScoreEntries
                .AsNoTracking()
                .Where(e => e.ArrInstanceId == arrInstance.Id && e.ItemType == InstanceType.Radarr)
                .ToDictionaryAsync(e => e.ExternalItemId);

            List<ArrQualityProfile> profiles = await _radarrClient.GetQualityProfilesAsync(arrInstance);
            upgradesDisabledProfileIds = profiles.Where(p => !p.UpgradeAllowed).Select(p => p.Id).ToHashSet();
        }

        DateTimeOffset graceCutoff = _timeProvider.GetUtcNow().AddHours(-config.PostReleaseGraceHours);
        
        List<long> allLibraryIds = new();
        List<SearchableMovie> released = new();
        await foreach (SearchableMovie movie in _radarrClient.StreamAllMoviesAsync(arrInstance, cancellationToken))
        {
            allLibraryIds.Add(movie.Id);

            if (movie.Status is not "released")
            {
                continue;
            }

            if (!IsMoviePastGracePeriod(movie, graceCutoff))
            {
                continue;
            }

            if (instanceConfig.MonitoredOnly && !movie.Monitored)
            {
                continue;
            }

            if (!PassesTagFilter(movie.Tags, tagsById, skipTagSet, includeTagSet))
            {
                continue;
            }

            released.Add(movie);
        }

        HashSet<long> missingIds;
        HashSet<long> cutoffUnmetIds = [];
        try
        {
            List<SearchableMovie> missing = await ReadWantedAsync(
                monitored => _radarrClient.GetMissingMoviesAsync(arrInstance, monitored, cancellationToken),
                instanceConfig.MonitoredOnly);
            missingIds = missing.Select(m => m.Id).ToHashSet();

            if (instanceConfig.UseCutoff)
            {
                List<SearchableMovie> cutoffUnmet = await ReadWantedAsync(
                    monitored => _radarrClient.GetCutoffUnmetMoviesAsync(arrInstance, monitored, cancellationToken),
                    instanceConfig.MonitoredOnly);
                cutoffUnmetIds = cutoffUnmet.Select(m => m.Id).ToHashSet();
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Skipping proactive search for {InstanceName}, it needs Radarr v5.6 or newer", arrInstance.Name);
            return new SeekerProcessResult { Candidates = [], AllLibraryIds = allLibraryIds, Cycle = cycle };
        }

        List<SearchableMovie> candidates = new();
        foreach (SearchableMovie movie in released)
        {
            bool belowCustomFormatCutoff = instanceConfig.UseCustomFormatScore && cfScores != null && cfScores.TryGetValue(movie.Id, out var entry) && entry.CurrentScore < entry.CutoffScore;
            bool upgradesDisabled = belowCustomFormatCutoff && upgradesDisabledProfileIds.Contains(movie.QualityProfileId);
            bool passesQualityCheck = missingIds.Contains(movie.Id)
                || cutoffUnmetIds.Contains(movie.Id)
                || (belowCustomFormatCutoff && !upgradesDisabled);
            if (!passesQualityCheck)
            {
                if (upgradesDisabled)
                {
                    _logger.LogDebug("skip | upgrades disabled in profile | {Title} | {InstanceName}", movie.Title, arrInstance.Name);
                }

                continue;
            }

            candidates.Add(movie);
        }

        int eligibleCount = candidates.Count;
        cycle.EligibleNow = eligibleCount;
        cycle.RemainingUnits = 0;

        if (candidates.Count == 0)
        {
            return await CompleteEmptyCycleAsync(cycle, instanceConfig, isDryRun, eligibleCount, arrInstance.Name, allLibraryIds);
        }

        // Exclude movies already in the download queue
        if (queuedMovieIds.Count > 0)
        {
            int beforeCount = candidates.Count;
            candidates = candidates
                .Where(m => !queuedMovieIds.Contains(m.Id))
                .ToList();

            int skipped = beforeCount - candidates.Count;
            if (skipped > 0)
            {
                _logger.LogDebug("Excluded {Count} movies already in queue on {InstanceName}",
                    skipped, arrInstance.Name);
            }

            if (candidates.Count == 0)
            {
                return await CompleteEmptyCycleAsync(cycle, instanceConfig, isDryRun, eligibleCount, arrInstance.Name, allLibraryIds);
            }
        }

        cycle.RemainingUnits = candidates.Count(m => !searchHistory.ContainsKey(m.Id));

        if (cycle.RemainingUnits == 0)
        {
            SeekerCycle? next = await CompleteCycleAsync(cycle, instanceConfig, isDryRun, eligibleCount, arrInstance.Name);
            if (next is null)
            {
                return new SeekerProcessResult { Candidates = [], AllLibraryIds = allLibraryIds, Cycle = cycle };
            }

            cycle = next;
            cycle.RemainingUnits = candidates.Count;
            searchHistory = new Dictionary<long, DateTimeOffset>();
        }

        // Only pass unsearched items to the selector — already-searched items in this cycle are skipped
        var selectionCandidates = candidates
            .Where(m => !searchHistory.ContainsKey(m.Id))
            .Select(m => (m.Id, m.Added, LastSearched: (DateTimeOffset?)null))
            .ToList();

        IItemSelector selector = ItemSelectorFactory.Create(config.SelectionStrategy);
        List<long> selectedIds = selector.Select(selectionCandidates, 1);

        Dictionary<long, SearchableMovie> candidatesById = candidates.ToDictionary(m => m.Id);

        List<SeekerSearchCandidate> searchCandidates = [];
        foreach (long movieId in selectedIds)
        {
            if (!candidatesById.TryGetValue(movieId, out SearchableMovie? movie))
            {
                continue;
            }
            SeekerSearchReason reason;
            if (missingIds.Contains(movieId))
            {
                reason = SeekerSearchReason.Missing;
            }
            else if (cutoffUnmetIds.Contains(movieId))
            {
                reason = SeekerSearchReason.QualityCutoffNotMet;
            }
            else
            {
                reason = SeekerSearchReason.CustomFormatScoreBelowCutoff;
            }

            searchCandidates.Add(new SeekerSearchCandidate
            {
                ItemId = movieId,
                Name = movie.Title,
                SeasonNumber = 0,
                Reason = reason,
            });

            _logger.LogDebug("Selected '{Title}' for search on {InstanceName}: {Reason}",
                movie.Title, arrInstance.Name, reason);
        }

        return new SeekerProcessResult { Candidates = searchCandidates, AllLibraryIds = allLibraryIds, Cycle = cycle };
    }

    private async Task<SeekerProcessResult> ProcessSonarrAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        SeekerInstanceConfig instanceConfig,
        Dictionary<long, DateTimeOffset> seriesSearchHistory,
        List<SeekerHistory> currentCycleHistory,
        SeekerCycle cycle,
        bool isDryRun,
        HashSet<(long SeriesId, long SeasonNumber)> queuedSeasons,
        HashSet<long> queuedEpisodeIds,
        CancellationToken cancellationToken)
    {
        List<Tag> tags = await _sonarrClient.GetAllTagsAsync(arrInstance);
        Dictionary<long, string> tagsById = tags.ToDictionary(t => t.Id, t => t.Label);
        HashSet<string> skipTagSet = new(instanceConfig.SkipTags, StringComparer.InvariantCultureIgnoreCase);
        HashSet<string> includeTagSet = new(instanceConfig.IncludeTags, StringComparer.InvariantCultureIgnoreCase);

        HashSet<int> upgradesDisabledProfileIds = [];
        if (instanceConfig.UseCustomFormatScore)
        {
            List<ArrQualityProfile> profiles = await _sonarrClient.GetQualityProfilesAsync(arrInstance);
            upgradesDisabledProfileIds = profiles.Where(p => !p.UpgradeAllowed).Select(p => p.Id).ToHashSet();
        }

        DateTimeOffset graceCutoff = _timeProvider.GetUtcNow().AddHours(-config.PostReleaseGraceHours);

        List<long> allLibraryIds = new();
        Dictionary<long, SearchableSeries> seriesById = new();
        await foreach (SearchableSeries series in _sonarrClient.StreamAllSeriesAsync(arrInstance, cancellationToken))
        {
            allLibraryIds.Add(series.Id);

            if (series.Status is not ("continuing" or "ended" or "released"))
            {
                continue;
            }

            if (instanceConfig.MonitoredOnly && !series.Monitored)
            {
                continue;
            }

            if (!PassesTagFilter(series.Tags, tagsById, skipTagSet, includeTagSet))
            {
                continue;
            }

            seriesById[series.Id] = series;
        }

        List<SonarrWantedEpisode> wanted = [];

        bool IsReleasedEpisodeOfCandidate(SearchableEpisode e) =>
            seriesById.ContainsKey(e.SeriesId) && e.AirDateUtc.HasValue && e.AirDateUtc.Value <= graceCutoff;

        SonarrWantedEpisode ToWanted(SearchableEpisode e, SeekerSearchReason reason) => new()
        {
            SeriesId = e.SeriesId,
            SeasonNumber = e.SeasonNumber,
            EpisodeId = e.Id,
            EpisodeNumber = e.EpisodeNumber,
            AirDateUtc = e.AirDateUtc,
            Reason = reason,
        };

        List<SearchableEpisode> missing = await ReadWantedAsync(
            monitored => _sonarrClient.GetMissingEpisodesAsync(arrInstance, monitored, cancellationToken),
            instanceConfig.MonitoredOnly);
        wanted.AddRange(missing
            .Where(IsReleasedEpisodeOfCandidate)
            .Select(e => ToWanted(e, SeekerSearchReason.Missing)));

        if (instanceConfig.UseCutoff)
        {
            List<SearchableEpisode> cutoffUnmet = await ReadWantedAsync(
                monitored => _sonarrClient.GetCutoffUnmetEpisodesAsync(arrInstance, monitored, cancellationToken),
                instanceConfig.MonitoredOnly);
            wanted.AddRange(cutoffUnmet
                .Where(IsReleasedEpisodeOfCandidate)
                .Select(e => ToWanted(e, SeekerSearchReason.QualityCutoffNotMet)));
        }

        if (instanceConfig.UseCustomFormatScore)
        {
            // Rows synced before the season/air date columns existed carry neither until the next sync.
            List<CustomFormatScoreEntry> belowCutoff = await _eventsContext.CustomFormatScoreEntries
                .AsNoTracking()
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == InstanceType.Sonarr
                    && e.SeasonNumber != null
                    && e.AirDateUtc != null
                    && e.AirDateUtc <= graceCutoff
                    && e.CurrentScore < e.CutoffScore
                    && (!instanceConfig.MonitoredOnly || e.IsMonitored))
                .ToListAsync(cancellationToken);

            foreach (CustomFormatScoreEntry entry in belowCutoff)
            {
                if (!seriesById.TryGetValue(entry.ExternalItemId, out SearchableSeries? series))
                {
                    continue;
                }

                if (upgradesDisabledProfileIds.Contains(series.QualityProfileId))
                {
                    _logger.LogDebug("skip | upgrades disabled in profile | {Title} | {InstanceName}", entry.Title, arrInstance.Name);
                    continue;
                }

                wanted.Add(new SonarrWantedEpisode
                {
                    SeriesId = entry.ExternalItemId,
                    SeasonNumber = entry.SeasonNumber!.Value,
                    EpisodeId = entry.EpisodeId,
                    Title = entry.Title,
                    AirDateUtc = entry.AirDateUtc,
                    Reason = SeekerSearchReason.CustomFormatScoreBelowCutoff,
                });
            }
        }

        HashSet<(long SeriesId, int SeasonNumber)> finishedAnimeSeasons = await GetFinishedAnimeSeasonsAsync(
            arrInstance, seriesById, wanted, cancellationToken);

        HashSet<(long SeriesId, int SeasonNumber)> searchedSeasons = currentCycleHistory
            .Where(h => h.EpisodeId == 0 && h.SeasonNumber > 0)
            .Select(h => (h.ExternalItemId, h.SeasonNumber))
            .ToHashSet();
        HashSet<long> searchedEpisodeIds = currentCycleHistory
            .Where(h => h.EpisodeId > 0)
            .Select(h => h.EpisodeId)
            .ToHashSet();

        List<SonarrSearchUnit> units = BuildSonarrUnits(wanted, seriesById, finishedAnimeSeasons, searchedSeasons, searchedEpisodeIds);

        bool IsQueued(SonarrSearchUnit unit) => unit.EpisodeId == 0
            ? queuedSeasons.Contains((unit.SeriesId, unit.SeasonNumber))
            : queuedEpisodeIds.Contains(unit.EpisodeId);

        cycle.EligibleNow = units.Count;
        cycle.RemainingUnits = 0;

        if (units.Count == 0)
        {
            return await CompleteEmptyCycleAsync(cycle, instanceConfig, isDryRun, units.Count, arrInstance.Name, allLibraryIds);
        }

        int queuedCount = units.Count(IsQueued);
        if (queuedCount > 0)
        {
            _logger.LogDebug("Excluded {Count} units already in queue on {InstanceName}",
                queuedCount, arrInstance.Name);
        }

        if (queuedCount == units.Count)
        {
            return await CompleteEmptyCycleAsync(cycle, instanceConfig, isDryRun, units.Count, arrInstance.Name, allLibraryIds);
        }

        List<SonarrSearchUnit> remaining = units
            .Where(u => !u.Searched && !IsQueued(u))
            .ToList();

        cycle.RemainingUnits = remaining.Count;

        if (remaining.Count == 0)
        {
            SeekerCycle? next = await CompleteCycleAsync(cycle, instanceConfig, isDryRun, units.Count, arrInstance.Name);
            if (next is null)
            {
                return new SeekerProcessResult { Candidates = [], AllLibraryIds = allLibraryIds, Cycle = cycle };
            }

            cycle = next;
            seriesSearchHistory = new Dictionary<long, DateTimeOffset>();
            units = BuildSonarrUnits(wanted, seriesById, finishedAnimeSeasons, [], []);
            remaining = units.Where(u => !IsQueued(u)).ToList();
            cycle.EligibleNow = units.Count;
            cycle.RemainingUnits = remaining.Count;

            if (remaining.Count == 0)
            {
                return new SeekerProcessResult { Candidates = [], AllLibraryIds = allLibraryIds, Cycle = cycle };
            }
        }

        Dictionary<long, List<SonarrSearchUnit>> remainingBySeries = remaining
            .GroupBy(u => u.SeriesId)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<(long Id, DateTimeOffset? Added, DateTimeOffset? LastSearched)> selectionCandidates = remainingBySeries.Keys
            .Select(id => (id, seriesById[id].Added, LastSearched: seriesSearchHistory.TryGetValue(id, out DateTimeOffset dt) ? (DateTimeOffset?)dt : null))
            .ToList();

        IItemSelector selector = ItemSelectorFactory.Create(config.SelectionStrategy);
        List<long> selectedIds = selector.Select(selectionCandidates, 1);

        List<SonarrSearchUnit> seriesUnits = remainingBySeries[selectedIds[0]];
        List<int> seriesSeasons = seriesUnits.Select(u => u.SeasonNumber).Distinct().ToList();
        int seasonNumber = seriesSeasons[Random.Shared.Next(seriesSeasons.Count)];
        SonarrSearchUnit unit = seriesUnits
            .Where(u => u.SeasonNumber == seasonNumber)
            .OrderBy(u => u.AirDateUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(u => u.EpisodeId)
            .First();

        int missingCount = unit.Reasons.Count(r => r == SeekerSearchReason.Missing);
        int cutoffCount = unit.Reasons.Count(r => r == SeekerSearchReason.QualityCutoffNotMet);
        int cfCount = unit.Reasons.Count(r => r == SeekerSearchReason.CustomFormatScoreBelowCutoff);

        List<string> reasons = [];
        if (missingCount > 0)
        {
            reasons.Add($"{missingCount} missing");
        }

        if (cutoffCount > 0)
        {
            reasons.Add($"{cutoffCount} cutoff unmet");
        }

        if (cfCount > 0)
        {
            reasons.Add($"{cfCount} below CF score cutoff");
        }

        SeekerSearchReason searchReason = missingCount > 0
            ? SeekerSearchReason.Missing
            : cutoffCount > 0
                ? SeekerSearchReason.QualityCutoffNotMet
                : SeekerSearchReason.CustomFormatScoreBelowCutoff;

        _logger.LogDebug("Selected '{Name}' ({ContentKind}) for search on {InstanceName}: {Reasons}",
            unit.Name, unit.ContentKind, arrInstance.Name, string.Join(", ", reasons));

        return new SeekerProcessResult
        {
            Candidates =
            [
                new SeekerSearchCandidate
                {
                    ItemId = unit.SeriesId,
                    Name = unit.Name,
                    SeasonNumber = unit.SeasonNumber,
                    EpisodeId = unit.EpisodeId,
                    ContentKind = unit.ContentKind,
                    Reason = searchReason,
                }
            ],
            AllLibraryIds = allLibraryIds,
            Cycle = cycle,
        };
    }

    /// <summary>
    /// Groups wanted episodes into season and episode units.
    /// Sonarr runs one query per episode for a specials or airing anime season search, so those get episode units.
    /// A season partly searched in this cycle continues by episode.
    /// </summary>
    private static List<SonarrSearchUnit> BuildSonarrUnits(
        List<SonarrWantedEpisode> wanted,
        Dictionary<long, SearchableSeries> seriesById,
        HashSet<(long SeriesId, int SeasonNumber)> finishedAnimeSeasons,
        HashSet<(long SeriesId, int SeasonNumber)> searchedSeasons,
        HashSet<long> searchedEpisodeIds)
    {
        List<SonarrSearchUnit> units = [];

        foreach (IGrouping<(long SeriesId, int SeasonNumber), SonarrWantedEpisode> season in wanted.GroupBy(w => (w.SeriesId, w.SeasonNumber)))
        {
            SearchableSeries series = seriesById[season.Key.SeriesId];
            SeekerContentKind kind = GetContentKind(series, season.Key.SeasonNumber);
            bool seasonSearched = searchedSeasons.Contains(season.Key);
            bool canSearchSeason = kind switch
            {
                SeekerContentKind.Special => false,
                SeekerContentKind.Anime => finishedAnimeSeasons.Contains(season.Key),
                _ => true,
            };

            if (canSearchSeason && (seasonSearched || !season.Any(w => searchedEpisodeIds.Contains(w.EpisodeId))))
            {
                units.Add(new SonarrSearchUnit
                {
                    SeriesId = season.Key.SeriesId,
                    SeasonNumber = season.Key.SeasonNumber,
                    EpisodeId = 0,
                    Name = $"{series.Title} S{season.Key.SeasonNumber:D2}",
                    ContentKind = kind,
                    Reasons = season.Select(w => w.Reason).ToList(),
                    Searched = seasonSearched,
                });
                continue;
            }

            foreach (IGrouping<long, SonarrWantedEpisode> episode in season.GroupBy(w => w.EpisodeId))
            {
                SonarrWantedEpisode first = episode.First();
                int? episodeNumber = episode.Select(w => w.EpisodeNumber).FirstOrDefault(n => n is not null);

                units.Add(new SonarrSearchUnit
                {
                    SeriesId = season.Key.SeriesId,
                    SeasonNumber = season.Key.SeasonNumber,
                    EpisodeId = episode.Key,
                    Name = episodeNumber is not null
                        ? $"{series.Title} S{season.Key.SeasonNumber:D2}E{episodeNumber:D2}"
                        : first.Title ?? $"{series.Title} S{season.Key.SeasonNumber:D2}",
                    ContentKind = kind,
                    Reasons = episode.Select(w => w.Reason).ToList(),
                    AirDateUtc = first.AirDateUtc,
                    Searched = seasonSearched || searchedEpisodeIds.Contains(episode.Key),
                });
            }
        }

        return units;
    }

    private static SeekerContentKind GetContentKind(SearchableSeries series, int seasonNumber)
    {
        if (seasonNumber == 0)
        {
            return SeekerContentKind.Special;
        }

        return series.SeriesType.ToLowerInvariant() switch
        {
            "anime" => SeekerContentKind.Anime,
            "daily" => SeekerContentKind.Daily,
            _ => SeekerContentKind.Standard,
        };
    }

    /// <summary>
    /// Finds the anime seasons Sonarr searches with one query: every episode airs within the next 24 hours.
    /// An episode without an air date keeps its season out, as in Sonarr.
    /// </summary>
    private async Task<HashSet<(long SeriesId, int SeasonNumber)>> GetFinishedAnimeSeasonsAsync(
        ArrInstance arrInstance,
        Dictionary<long, SearchableSeries> seriesById,
        List<SonarrWantedEpisode> wanted,
        CancellationToken cancellationToken)
    {
        List<long> animeSeriesIds = wanted
            .Where(w => w.SeasonNumber > 0
                        && GetContentKind(seriesById[w.SeriesId], w.SeasonNumber) is SeekerContentKind.Anime)
            .Select(w => w.SeriesId)
            .Distinct()
            .ToList();

        DateTimeOffset airedBy = _timeProvider.GetUtcNow().AddHours(24);
        HashSet<(long SeriesId, int SeasonNumber)> finished = [];

        foreach (long seriesId in animeSeriesIds)
        {
            List<SearchableEpisode> episodes;
            try
            {
                episodes = await _sonarrClient.GetEpisodesAsync(arrInstance, seriesId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Searching {Title} by episode, failed to read its episodes | {InstanceName}",
                    seriesById[seriesId].Title, arrInstance.Name);
                continue;
            }

            finished.UnionWith(episodes
                .GroupBy(e => e.SeasonNumber)
                .Where(g => g.All(e => e.AirDateUtc.HasValue && e.AirDateUtc.Value <= airedBy))
                .Select(g => (seriesId, g.Key)));
        }

        return finished;
    }

    /// <summary>
    /// Reads a wanted list for monitored items, plus unmonitored ones when the instance searches those too.
    /// </summary>
    private static async Task<List<T>> ReadWantedAsync<T>(Func<bool, Task<List<T>>> readWanted, bool monitoredOnly)
    {
        List<T> records = await readWanted(true);

        if (!monitoredOnly)
        {
            records.AddRange(await readWanted(false));
        }

        return records;
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
        string instanceName)
    {
        // An empty cycle stays open, so an instance with nothing eligible does not complete a cycle every week.
        if (cycle.SearchedUnits == 0)
        {
            return null;
        }

        if (cycle.CompletedAt is null)
        {
            cycle.CompletedAt = _timeProvider.GetUtcNow();
            cycle.CompletionReason = SeekerCycleCompletionReason.Completed;

            _logger.LogInformation("Cycle completed on {InstanceName} after {Count} searches",
                instanceName, cycle.SearchedUnits);
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
    /// searched something, so a cycle with nothing left does not stay "in progress" forever.
    /// </summary>
    private async Task<SeekerProcessResult> CompleteEmptyCycleAsync(
        SeekerCycle cycle,
        SeekerInstanceConfig instanceConfig,
        bool isDryRun,
        int eligibleCount,
        string instanceName,
        List<long> allLibraryIds)
    {
        SeekerCycle? next = await CompleteCycleAsync(cycle, instanceConfig, isDryRun, eligibleCount, instanceName);
        return new SeekerProcessResult { Candidates = [], AllLibraryIds = allLibraryIds, Cycle = next ?? cycle };
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

    /// <summary>
    /// Returns true when the movie's release date is past the grace period cutoff.
    /// Movies without any release date info are treated as released.
    /// </summary>
    private static bool IsMoviePastGracePeriod(SearchableMovie movie, DateTimeOffset graceCutoff)
    {
        DateTimeOffset? releaseDate = movie.DigitalRelease ?? movie.PhysicalRelease ?? movie.InCinemas;
        return releaseDate is null || releaseDate.Value <= graceCutoff;
    }

    private static bool PassesTagFilter(
        List<long> itemTagIds,
        Dictionary<long, string> tagsById,
        HashSet<string> skipTags,
        HashSet<string> includeTags)
    {
        List<string> labels = itemTagIds
            .Select(id => tagsById.GetValueOrDefault(id))
            .OfType<string>()
            .ToList();

        if (labels.Any(skipTags.Contains))
        {
            return false;
        }

        return includeTags.Count == 0 || labels.Any(includeTags.Contains);
    }
}
