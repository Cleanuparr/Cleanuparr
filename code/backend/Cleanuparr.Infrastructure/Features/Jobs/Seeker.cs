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
using Cleanuparr.Infrastructure.Hubs;
using Data.Models.Arr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    private readonly ILogger<Seeker> _logger;
    private readonly DataContext _dataContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IArrQueueIterator _arrQueueIterator;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly IHostingEnvironment _environment;
    private readonly TimeProvider _timeProvider;
    private readonly IHubContext<AppHub> _hubContext;

    public Seeker(
        ILogger<Seeker> logger,
        DataContext dataContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        IArrClientFactory arrClientFactory,
        IArrQueueIterator arrQueueIterator,
        IEventPublisher eventPublisher,
        IDryRunInterceptor dryRunInterceptor,
        IHostingEnvironment environment,
        TimeProvider timeProvider,
        IHubContext<AppHub> hubContext)
    {
        _logger = logger;
        _dataContext = dataContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _arrClientFactory = arrClientFactory;
        _arrQueueIterator = arrQueueIterator;
        _eventPublisher = eventPublisher;
        _dryRunInterceptor = dryRunInterceptor;
        _environment = environment;
        _timeProvider = timeProvider;
        _hubContext = hubContext;
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
        SearchQueueItem? replacementItem = await _dataContext.SearchQueue
            .OrderBy(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        if (replacementItem is not null)
        {
            await ProcessReplacementItemAsync(replacementItem, isDryRun);
            await _hubContext.Clients.All.SendAsync("SearchStatsUpdated");
            return;
        }

        // Missing items and quality upgrades
        if (!config.ProactiveSearchEnabled)
        {
            return;
        }

        await ProcessProactiveSearchAsync(config, isDryRun);

        await _hubContext.Clients.All.SendAsync("SearchStatsUpdated");
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
            _dataContext.SearchQueue.Remove(item);
            await _dataContext.SaveChangesAsync();
            return;
        }

        ContextProvider.Set(nameof(InstanceType), item.ArrInstance.ArrConfig.Type);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceId, arrInstance.Id);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, arrInstance.ExternalUrl ?? arrInstance.Url);

        try
        {
            IArrClient arrClient = _arrClientFactory.GetClient(item.ArrInstance.ArrConfig.Type, arrInstance.Version);
            HashSet<SearchItem> searchItems = BuildSearchItems(item);

            List<long> commandIds = await arrClient.SearchItemsAsync(arrInstance, searchItems);

            Guid eventId = await _eventPublisher.PublishSearchTriggered(item.Title, SeekerSearchType.Replacement, SeekerSearchReason.Replacement);

            if (!isDryRun)
            {
                await SaveCommandTrackersAsync(commandIds, eventId, arrInstance.Id, item.ArrInstance.ArrConfig.Type, item.ItemId, item.Title);
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
                _dataContext.SearchQueue.Remove(item);
                await _dataContext.SaveChangesAsync();
            }
        }
    }

    private static HashSet<SearchItem> BuildSearchItems(SearchQueueItem item)
    {
        if (item.SeriesId.HasValue && Enum.TryParse<SeriesSearchType>(item.SearchType, out var searchType))
        {
            return
            [
                new SeriesSearchItem
                {
                    Id = item.ItemId,
                    SeriesId = item.SeriesId.Value,
                    SearchType = searchType
                }
            ];
        }

        return [new SearchItem { Id = item.ItemId }];
    }

    private async Task ProcessProactiveSearchAsync(SeekerConfig config, bool isDryRun)
    {
        List<SeekerInstanceConfig> instanceConfigs = await _dataContext.SeekerInstanceConfigs
            .Include(s => s.ArrInstance)
                .ThenInclude(a => a.ArrConfig)
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
                .OrderBy(s => s.LastProcessedAt ?? DateTime.MinValue)
                .ToList();

            foreach (SeekerInstanceConfig instance in ordered)
            {
                bool searched = await ProcessSingleInstanceAsync(config, instance, isDryRun);
                
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
                await ProcessSingleInstanceAsync(config, instanceConfig, isDryRun);
            }
        }
    }

    private async Task<bool> ProcessSingleInstanceAsync(SeekerConfig config, SeekerInstanceConfig instanceConfig, bool isDryRun)
    {
        ArrInstance arrInstance = instanceConfig.ArrInstance;
        InstanceType instanceType = arrInstance.ArrConfig.Type;

        _logger.LogDebug("Processing {InstanceType} instance: {InstanceName}",
            instanceType, arrInstance.Name);

        // Set context for event publishing
        ContextProvider.Set(nameof(InstanceType), instanceType);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceId, arrInstance.Id);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, arrInstance.ExternalUrl ?? arrInstance.Url);

        // Fetch queue once for both active download limit check and queue cross-referencing
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType, arrInstance.Version);
        List<QueueRecord> queueRecords = [];

        try
        {
            await _arrQueueIterator.Iterate(arrClient, arrInstance, records =>
            {
                queueRecords.AddRange(records);
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch queue for {InstanceName}, proceeding without queue cross-referencing",
                arrInstance.Name);
        }

        // Check active download limit using the fetched queue data
        if (instanceConfig.ActiveDownloadLimit > 0)
        {
            int activeDownloads = queueRecords
                .Where(r => r.SizeLeft > 0)
                .Select(r => r.DownloadId)
                .Distinct()
                .Count();
            if (activeDownloads >= instanceConfig.ActiveDownloadLimit)
            {
                _logger.LogInformation(
                    "Skipping proactive search for {InstanceName} — {Count} items actively downloading (limit: {Limit})",
                    arrInstance.Name, activeDownloads, instanceConfig.ActiveDownloadLimit);
                return false;
            }
        }

        bool searched = false;
        try
        {
            searched = await ProcessInstanceAsync(config, instanceConfig, arrInstance, instanceType, isDryRun, queueRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {InstanceType} instance: {InstanceName}",
                instanceType, arrInstance.Name);
        }

        // Update LastProcessedAt so round-robin moves on
        instanceConfig.LastProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        _dataContext.SeekerInstanceConfigs.Update(instanceConfig);
        await _dataContext.SaveChangesAsync();

        return searched;
    }

    private async Task<bool> ProcessInstanceAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        InstanceType instanceType,
        bool isDryRun,
        List<QueueRecord> queueRecords)
    {
        // Load search history for the current cycle
        List<SeekerHistory> currentCycleHistory = await _dataContext.SeekerHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == arrInstance.Id && h.CycleId == instanceConfig.CurrentCycleId)
            .ToListAsync();

        // Load all history for stale cleanup
        List<long> allHistoryExternalIds = await _dataContext.SeekerHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == arrInstance.Id)
            .Select(x => x.ExternalItemId)
            .ToListAsync();

        // Derive item-level history for selection strategies
        Dictionary<long, DateTime> itemSearchHistory = currentCycleHistory
            .GroupBy(h => h.ExternalItemId)
            .ToDictionary(g => g.Key, g => g.Max(h => h.LastSearchedAt));

        // Build queued-item lookups from active queue records
        var activeQueueRecords = queueRecords
            .Where(r => ActiveQueueStates.Contains(r.TrackedDownloadState))
            .ToList();

        HashSet<SearchItem> searchItems;
        List<string> selectedNames;
        List<long> allLibraryIds;
        List<long> historyIds;
        int seasonNumber = 0;
        SeekerSearchReason searchReason;

        if (instanceType == InstanceType.Radarr)
        {
            HashSet<long> queuedMovieIds = activeQueueRecords
                .Where(r => r.MovieId > 0)
                .Select(r => r.MovieId)
                .ToHashSet();

            List<long> selectedIds;
            (selectedIds, selectedNames, allLibraryIds, searchReason) = await ProcessRadarrAsync(config, arrInstance, instanceConfig, itemSearchHistory, isDryRun, queuedMovieIds);
            searchItems = selectedIds.Select(id => new SearchItem { Id = id }).ToHashSet();
            historyIds = selectedIds;
        }
        else
        {
            HashSet<(long SeriesId, long SeasonNumber)> queuedSeasons = activeQueueRecords
                .Where(r => r.SeriesId > 0)
                .Select(r => (r.SeriesId, r.SeasonNumber))
                .ToHashSet();

            (searchItems, selectedNames, allLibraryIds, historyIds, seasonNumber, searchReason) =
                await ProcessSonarrAsync(config, arrInstance, instanceConfig, itemSearchHistory, currentCycleHistory, isDryRun, queuedSeasons: queuedSeasons);
        }

        if (searchItems.Count == 0)
        {
            _logger.LogDebug("No items selected for search on {InstanceName}", arrInstance.Name);
            if (!isDryRun)
            {
                await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, allLibraryIds, allHistoryExternalIds);
            }
            return false;
        }

        // Search each item individually so each gets its own event and command tracker
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType, arrInstance.Version);
        var searchItemsList = searchItems.ToList();

        for (int i = 0; i < searchItemsList.Count; i++)
        {
            SearchItem searchItem = searchItemsList[i];
            string itemName = selectedNames[i];
            long historyId = historyIds[i];

            List<long> commandIds = await arrClient.SearchItemsAsync(arrInstance, [searchItem]);

            Guid eventId = await _eventPublisher.PublishSearchTriggered(
                itemName, SeekerSearchType.Proactive, searchReason, instanceConfig.CurrentCycleId);

            _logger.LogInformation("Search triggered for {Item} | {InstanceUrl}", itemName, arrInstance.Url);

            await UpdateSearchHistoryAsync(arrInstance.Id, instanceType, instanceConfig.CurrentCycleId,
                [historyId], [itemName], seasonNumber, isDryRun);

            if (!isDryRun)
            {
                await SaveCommandTrackersAsync(commandIds, eventId, arrInstance.Id, instanceType,
                    historyId, itemName, seasonNumber);
            }
        }

        if (!isDryRun)
        {
            await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, allLibraryIds, allHistoryExternalIds);
            await CleanupOldCycleHistoryAsync(arrInstance, instanceConfig.CurrentCycleId);
        }

        return true;
    }

    private async Task<(List<long> SelectedIds, List<string> SelectedNames, List<long> AllLibraryIds, SeekerSearchReason SearchReason)> ProcessRadarrAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        SeekerInstanceConfig instanceConfig,
        Dictionary<long, DateTime> searchHistory,
        bool isDryRun,
        HashSet<long> queuedMovieIds)
    {
        List<SearchableMovie> movies = await _radarrClient.GetAllMoviesAsync(arrInstance);
        List<Tag> tags = await _radarrClient.GetAllTagsAsync(arrInstance);
        List<long> allLibraryIds = movies.Select(m => m.Id).ToList();

        Dictionary<long, string> tagsById = tags.ToDictionary(t => t.Id, t => t.Label);
        HashSet<string> skipTagSet = new(instanceConfig.SkipTags, StringComparer.InvariantCultureIgnoreCase);

        // Load cached CF scores when custom format score filtering is enabled
        Dictionary<long, CustomFormatScoreEntry>? cfScores = null;
        if (config.UseCustomFormatScore)
        {
            cfScores = await _dataContext.CustomFormatScoreEntries
                .AsNoTracking()
                .Where(e => e.ArrInstanceId == arrInstance.Id && e.ItemType == InstanceType.Radarr)
                .ToDictionaryAsync(e => e.ExternalItemId);
        }

        // Apply filters — UseCutoff and UseCustomFormatScore are OR-ed: an item qualifies if it fails the quality cutoff OR the CF score cutoff.
        // Items without cutoff data or a cached CF score are excluded from the respective filter.
        DateTime graceCutoff = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-config.PostReleaseGraceHours);
        var candidates = movies
            .Where(m => m.Status is "released")
            .Where(m => IsMoviePastGracePeriod(m, graceCutoff))
            .Where(m => !config.MonitoredOnly || m.Monitored)
            .Where(m => instanceConfig.SkipTags.Count == 0 ||
                !m.Tags
                    .Select(id => tagsById.TryGetValue(id, out var label) ? label : null)
                    .Any(label => label is not null && skipTagSet.Contains(label))
            )
            .Where(m => !m.HasFile
                || (!config.UseCutoff && !config.UseCustomFormatScore)
                || (config.UseCutoff && (m.MovieFile?.QualityCutoffNotMet ?? false))
                || (config.UseCustomFormatScore && cfScores != null && cfScores.TryGetValue(m.Id, out var entry) && entry.CurrentScore < entry.CutoffScore))
            .ToList();

        instanceConfig.TotalEligibleItems = candidates.Count;

        if (candidates.Count == 0)
        {
            return ([], [], allLibraryIds, default);
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
                return ([], [], allLibraryIds, default);
            }
        }

        // Check for cycle completion: all candidates already searched in current cycle
        bool cycleComplete = candidates.All(m => searchHistory.ContainsKey(m.Id));
        if (cycleComplete)
        {
            // Respect MinCycleTimeDays even when cycle completes due to queue filtering
            DateTime? cycleStartedAt = searchHistory.Count > 0 ? searchHistory.Values.Min() : null;
            if (ShouldWaitForMinCycleTime(instanceConfig, cycleStartedAt))
            {
                _logger.LogDebug(
                    "skip | cycle complete but min time ({Days}) not elapsed (started {StartedAt}) | {InstanceName}",
                    instanceConfig.MinCycleTimeDays, cycleStartedAt, arrInstance.Name);
                return ([], [], allLibraryIds, default);
            }

            _logger.LogInformation("All {Count} items on {InstanceName} searched in current cycle, starting new cycle",
                candidates.Count, arrInstance.Name);

            if (!isDryRun)
            {
                instanceConfig.CurrentCycleId = Guid.NewGuid();
                _dataContext.SeekerInstanceConfigs.Update(instanceConfig);
                await _dataContext.SaveChangesAsync();
            }

            searchHistory = new Dictionary<long, DateTime>();
        }

        // Only pass unsearched items to the selector — already-searched items in this cycle are skipped
        var selectionCandidates = candidates
            .Where(m => !searchHistory.ContainsKey(m.Id))
            .Select(m => (m.Id, m.Added, LastSearched: (DateTime?)null))
            .ToList();

        IItemSelector selector = ItemSelectorFactory.Create(config.SelectionStrategy);
        List<long> selectedIds = selector.Select(selectionCandidates, 1);

        List<string> selectedNames = candidates
            .Where(m => selectedIds.Contains(m.Id))
            .Select(m => m.Title)
            .ToList();

        // Determine the search reason from the first selected movie
        SearchableMovie selectedMovie = candidates.First(m => m.Id == selectedIds[0]);
        SeekerSearchReason searchReason = !selectedMovie.HasFile
            ? SeekerSearchReason.Missing
            : config.UseCutoff && (selectedMovie.MovieFile?.QualityCutoffNotMet ?? false)
                ? SeekerSearchReason.QualityCutoffNotMet
                : SeekerSearchReason.CustomFormatScoreBelowCutoff;

        foreach (long movieId in selectedIds)
        {
            SearchableMovie movie = candidates.First(m => m.Id == movieId);
            _logger.LogDebug("Selected '{Title}' for search on {InstanceName}: {Reason}",
                movie.Title, arrInstance.Name, searchReason);
        }

        return (selectedIds, selectedNames, allLibraryIds, searchReason);
    }

    private async Task<(HashSet<SearchItem> SearchItems, List<string> SelectedNames, List<long> AllLibraryIds, List<long> HistoryIds, int SeasonNumber, SeekerSearchReason SearchReason)> ProcessSonarrAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        SeekerInstanceConfig instanceConfig,
        Dictionary<long, DateTime> seriesSearchHistory,
        List<SeekerHistory> currentCycleHistory,
        bool isDryRun,
        bool isRetry = false,
        HashSet<(long SeriesId, long SeasonNumber)>? queuedSeasons = null)
    {
        List<SearchableSeries> series = await _sonarrClient.GetAllSeriesAsync(arrInstance);
        List<Tag> tags = await _sonarrClient.GetAllTagsAsync(arrInstance);
        List<long> allLibraryIds = series.Select(s => s.Id).ToList();
        DateTime graceCutoff = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-config.PostReleaseGraceHours);

        Dictionary<long, string> tagsById = tags.ToDictionary(t => t.Id, t => t.Label);
        HashSet<string> skipTagSet = new(instanceConfig.SkipTags, StringComparer.InvariantCultureIgnoreCase);

        // Apply filters
        var candidates = series
            .Where(s => s.Status is "continuing" or "ended" or "released")
            .Where(s => !config.MonitoredOnly || s.Monitored)
            .Where(s => instanceConfig.SkipTags.Count == 0 ||
                !s.Tags
                    .Select(id => tagsById.TryGetValue(id, out var label) ? label : null)
                    .Any(label => label is not null && skipTagSet.Contains(label))
            )
            // Skip fully-downloaded series (unless quality upgrade filters active)
            .Where(s => config.UseCutoff || config.UseCustomFormatScore
                || s.Statistics == null || s.Statistics.EpisodeCount == 0
                || s.Statistics.EpisodeFileCount < s.Statistics.EpisodeCount)
            .ToList();

        instanceConfig.TotalEligibleItems = candidates.Count;

        if (candidates.Count == 0)
        {
            return ([], [], allLibraryIds, [], 0, default);
        }

        // Pass all candidates — BuildSonarrSearchItemAsync handles season-level exclusion
        // LastSearched info helps the selector deprioritize recently-searched series
        var selectionCandidates = candidates
            .Select(s => (s.Id, s.Added, LastSearched: seriesSearchHistory.TryGetValue(s.Id, out var dt) ? (DateTime?)dt : null))
            .ToList();

        // Select all candidates in priority order so the loop can find one with unsearched seasons
        IItemSelector selector = ItemSelectorFactory.Create(config.SelectionStrategy);
        List<long> candidateIds = selector.Select(selectionCandidates, selectionCandidates.Count);

        // Drill down to find the first series with qualifying unsearched seasons
        foreach (long seriesId in candidateIds)
        {
            string seriesTitle = string.Empty;

            try
            {
                List<SeekerHistory> seriesHistory = currentCycleHistory
                    .Where(h => h.ExternalItemId == seriesId)
                    .ToList();

                seriesTitle = candidates.First(s => s.Id == seriesId).Title;

                (SeriesSearchItem? searchItem, SearchableEpisode? selectedEpisode, SeekerSearchReason searchReason) =
                    await BuildSonarrSearchItemAsync(config, arrInstance, seriesId, seriesHistory, seriesTitle, graceCutoff, queuedSeasons);

                if (searchItem is not null)
                {
                    string displayName = $"{seriesTitle} S{searchItem.Id:D2}";
                    int seasonNumber = (int)searchItem.Id;

                    return ([searchItem], [displayName], allLibraryIds, [seriesId], seasonNumber, searchReason);
                }

                _logger.LogDebug("Skipping '{SeriesTitle}' — no qualifying seasons found", seriesTitle);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check episodes for '{SeriesTitle}', skipping", seriesTitle);
            }
        }

        // All candidates were tried and none had qualifying unsearched seasons — cycle complete
        if (candidates.Count > 0 && !isRetry)
        {
            // Respect MinCycleTimeDays even when cycle completes due to queue filtering
            DateTime? cycleStartedAt = seriesSearchHistory.Count > 0 ? seriesSearchHistory.Values.Min() : null;
            if (ShouldWaitForMinCycleTime(instanceConfig, cycleStartedAt))
            {
                _logger.LogDebug(
                    "skip | cycle complete but min time ({Days}) not elapsed (started {StartedAt}) | {InstanceName}",
                    instanceConfig.MinCycleTimeDays, cycleStartedAt, arrInstance.Name);
                return ([], [], allLibraryIds, [], 0, default);
            }

            _logger.LogInformation("All {Count} series on {InstanceName} searched in current cycle, starting new cycle",
                candidates.Count, arrInstance.Name);
            if (!isDryRun)
            {
                instanceConfig.CurrentCycleId = Guid.NewGuid();
                _dataContext.SeekerInstanceConfigs.Update(instanceConfig);
                await _dataContext.SaveChangesAsync();
            }

            // Retry with fresh cycle (only once to prevent infinite recursion)
            return await ProcessSonarrAsync(config, arrInstance, instanceConfig,
                new Dictionary<long, DateTime>(), [], isDryRun, isRetry: true, queuedSeasons: queuedSeasons);
        }

        return ([], [], allLibraryIds, [], 0, default);
    }

    /// <summary>
    /// Fetches episodes for a series and builds a season-level search item.
    /// Uses search history to prefer least-recently-searched seasons.
    /// </summary>
    private async Task<(SeriesSearchItem? SearchItem, SearchableEpisode? SelectedEpisode, SeekerSearchReason SearchReason)> BuildSonarrSearchItemAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        long seriesId,
        List<SeekerHistory> seriesHistory,
        string seriesTitle,
        DateTime graceCutoff,
        HashSet<(long SeriesId, long SeasonNumber)>? queuedSeasons = null)
    {
        List<SearchableEpisode> episodes = await _sonarrClient.GetEpisodesAsync(arrInstance, seriesId);

        // Fetch episode file metadata to determine cutoff status from the dedicated episodefile endpoint
        HashSet<long> cutoffNotMetFileIds = [];
        if (config.UseCutoff)
        {
            List<ArrEpisodeFile> episodeFiles = await _sonarrClient.GetEpisodeFilesAsync(arrInstance, seriesId);
            cutoffNotMetFileIds = episodeFiles
                .Where(f => f.QualityCutoffNotMet)
                .Select(f => f.Id)
                .ToHashSet();
        }

        // Load cached CF scores for this series when custom format score filtering is enabled
        Dictionary<long, CustomFormatScoreEntry>? cfScores = null;
        if (config.UseCustomFormatScore)
        {
            cfScores = await _dataContext.CustomFormatScoreEntries
                .AsNoTracking()
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == InstanceType.Sonarr
                    && e.ExternalItemId == seriesId)
                .ToDictionaryAsync(e => e.EpisodeId);
        }

        // Filter to qualifying episodes — UseCutoff and UseCustomFormatScore are OR-ed.
        // Cutoff status comes from the episodefile endpoint; items without a cached CF score are excluded.
        var qualifying = episodes
            .Where(e => e.AirDateUtc.HasValue && e.AirDateUtc.Value <= graceCutoff)
            .Where(e => !config.MonitoredOnly || e.Monitored)
            .Where(e => !e.HasFile
                || (!config.UseCutoff && !config.UseCustomFormatScore)
                || (config.UseCutoff && cutoffNotMetFileIds.Contains(e.EpisodeFileId))
                || (config.UseCustomFormatScore && cfScores != null && cfScores.TryGetValue(e.Id, out var entry) && entry.CurrentScore < entry.CutoffScore))
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToList();

        if (qualifying.Count == 0)
        {
            return (null, null, default);
        }

        // Select least-recently-searched season using history
        var seasonGroups = qualifying
            .GroupBy(e => e.SeasonNumber)
            .Select(g =>
            {
                DateTime? lastSearched = seriesHistory
                    .FirstOrDefault(h => h.SeasonNumber == g.Key)
                    ?.LastSearchedAt;
                return (SeasonNumber: g.Key, LastSearched: lastSearched, FirstEpisode: g.First());
            })
            .ToList();

        // Find unsearched seasons first
        var unsearched = seasonGroups.Where(s => s.LastSearched is null).ToList();

        // Exclude seasons already in the download queue
        if (queuedSeasons is { Count: > 0 })
        {
            int beforeCount = unsearched.Count;
            unsearched = unsearched
                .Where(s => !queuedSeasons.Contains((seriesId, (long)s.SeasonNumber)))
                .ToList();

            int skipped = beforeCount - unsearched.Count;
            if (skipped > 0)
            {
                _logger.LogDebug("Excluded {Count} seasons already in queue for '{SeriesTitle}' on {InstanceName}",
                    skipped, seriesTitle, arrInstance.Name);
            }
        }

        if (unsearched.Count == 0)
        {
            // All unsearched seasons are either searched or in the queue
            return (null, null, default);
        }

        // Pick from unsearched seasons with some randomization
        var selected = unsearched
            .OrderBy(_ => Random.Shared.Next())
            .First();

        // Log why this season was selected
        var seasonEpisodes = qualifying.Where(e => e.SeasonNumber == selected.SeasonNumber).ToList();
        int missingCount = seasonEpisodes.Count(e => !e.HasFile);
        int cutoffCount = seasonEpisodes.Count(e => e.HasFile && cutoffNotMetFileIds.Contains(e.EpisodeFileId));
        int cfCount = seasonEpisodes.Count(e => e.HasFile && cfScores != null
            && cfScores.TryGetValue(e.Id, out var cfEntry) && cfEntry.CurrentScore < cfEntry.CutoffScore);

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

        // Determine the primary search reason
        SeekerSearchReason searchReason = missingCount > 0
            ? SeekerSearchReason.Missing
            : cutoffCount > 0
                ? SeekerSearchReason.QualityCutoffNotMet
                : SeekerSearchReason.CustomFormatScoreBelowCutoff;

        _logger.LogDebug("Selected '{SeriesTitle}' S{Season:D2} for search on {InstanceName}: {Reasons}",
            seriesTitle, selected.SeasonNumber, arrInstance.Name, string.Join(", ", reasons));

        SeriesSearchItem searchItem = new()
        {
            Id = selected.SeasonNumber,
            SeriesId = seriesId,
            SearchType = SeriesSearchType.Season
        };

        return (searchItem, selected.FirstEpisode, searchReason);
    }

    private async Task UpdateSearchHistoryAsync(
        Guid arrInstanceId,
        InstanceType instanceType,
        Guid cycleId,
        List<long> searchedIds,
        List<string>? itemTitles = null,
        int seasonNumber = 0,
        bool isDryRun = false)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        for (int i = 0; i < searchedIds.Count; i++)
        {
            long id = searchedIds[i];
            string title = itemTitles != null && i < itemTitles.Count ? itemTitles[i] : string.Empty;

            SeekerHistory? existing = await _dataContext.SeekerHistory
                .FirstOrDefaultAsync(h =>
                    h.ArrInstanceId == arrInstanceId
                    && h.ExternalItemId == id
                    && h.ItemType == instanceType
                    && h.SeasonNumber == seasonNumber
                    && h.CycleId == cycleId);

            if (existing is not null)
            {
                existing.LastSearchedAt = now;
                existing.SearchCount++;
                if (!string.IsNullOrEmpty(title))
                {
                    existing.ItemTitle = title;
                }
            }
            else
            {
                _dataContext.SeekerHistory.Add(new SeekerHistory
                {
                    ArrInstanceId = arrInstanceId,
                    ExternalItemId = id,
                    ItemType = instanceType,
                    SeasonNumber = seasonNumber,
                    CycleId = cycleId,
                    LastSearchedAt = now,
                    ItemTitle = title,
                    IsDryRun = isDryRun,
                });
            }
        }

        await _dataContext.SaveChangesAsync();
    }

    private async Task SaveCommandTrackersAsync(
        List<long> commandIds,
        Guid eventId,
        Guid arrInstanceId,
        InstanceType instanceType,
        long externalItemId,
        string itemTitle,
        int seasonNumber = 0)
    {
        if (commandIds.Count == 0)
        {
            return;
        }

        foreach (long commandId in commandIds)
        {
            _dataContext.SeekerCommandTrackers.Add(new SeekerCommandTracker
            {
                ArrInstanceId = arrInstanceId,
                CommandId = commandId,
                EventId = eventId,
                ExternalItemId = externalItemId,
                ItemTitle = itemTitle,
                ItemType = instanceType,
                SeasonNumber = seasonNumber,
            });
        }

        await _dataContext.SaveChangesAsync();
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

        await _dataContext.SeekerHistory
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
    /// Removes history entries from previous cycles that are older than 30 days.
    /// Recent cycle history is retained for statistics and history viewing.
    /// </summary>
    private async Task CleanupOldCycleHistoryAsync(ArrInstance arrInstance, Guid currentCycleId)
    {
        DateTime cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-30);

        int deleted = await _dataContext.SeekerHistory
            .Where(h => h.ArrInstanceId == arrInstance.Id
                && h.CycleId != currentCycleId
                && h.LastSearchedAt < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogDebug("Cleaned up {Count} old cycle history entries (>30 days) for instance {InstanceName}",
                deleted, arrInstance.Name);
        }
    }

    /// <summary>
    /// Checks whether the minimum cycle time constraint prevents starting a new cycle.
    /// Returns true if the cycle started recently and MinCycleTimeDays has not yet elapsed.
    /// </summary>
    private bool ShouldWaitForMinCycleTime(SeekerInstanceConfig instanceConfig, DateTime? cycleStartedAt)
    {
        if (cycleStartedAt is null)
        {
            return false;
        }

        var elapsed = _timeProvider.GetUtcNow().UtcDateTime - cycleStartedAt.Value;
        return elapsed.TotalDays < instanceConfig.MinCycleTimeDays;
    }

    /// <summary>
    /// Returns true when the movie's release date is past the grace period cutoff.
    /// Movies without any release date info are treated as released.
    /// </summary>
    private static bool IsMoviePastGracePeriod(SearchableMovie movie, DateTime graceCutoff)
    {
        DateTime? releaseDate = movie.DigitalRelease ?? movie.PhysicalRelease ?? movie.InCinemas;
        return releaseDate is null || releaseDate.Value <= graceCutoff;
    }

}
