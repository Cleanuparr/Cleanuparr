using Cleanuparr.Domain.Entities.Arr;
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
using Data.Models.Arr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class Seeker : IHandler
{
    private const double JitterFactor = 0.8;

    private readonly ILogger<Seeker> _logger;
    private readonly DataContext _dataContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly IHostingEnvironment _environment;
    private readonly TimeProvider _timeProvider;

    public Seeker(
        ILogger<Seeker> logger,
        DataContext dataContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        IArrClientFactory arrClientFactory,
        IEventPublisher eventPublisher,
        IDryRunInterceptor dryRunInterceptor,
        IHostingEnvironment environment,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _arrClientFactory = arrClientFactory;
        _eventPublisher = eventPublisher;
        _dryRunInterceptor = dryRunInterceptor;
        _environment = environment;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync()
    {
        SeekerConfig config = await _dataContext.SeekerConfigs
            .AsNoTracking()
            .FirstAsync();

        if (!config.SearchEnabled)
        {
            _logger.LogDebug("Search is disabled");
            return;
        }

        await ApplyJitter(config);

        bool isDryRun = await _dryRunInterceptor.IsDryRunEnabled();

        // Replacement searches queued after download removal
        SearchQueueItem? replacementItem = await _dataContext.SearchQueue
            .OrderBy(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        if (replacementItem is not null)
        {
            await ProcessReplacementItemAsync(replacementItem, isDryRun);
            return;
        }

        // Missing items and quality upgrades
        if (!config.ProactiveSearchEnabled)
        {
            return;
        }

        await ProcessProactiveSearchAsync(config, isDryRun);
    }

    private async Task ApplyJitter(SeekerConfig config)
    {
        if (_environment.IsDevelopment())
        {
            return;
        }
        
        int maxJitterSeconds = (int)(config.SearchInterval * 60 * JitterFactor);
        int jitterSeconds = Random.Shared.Next(0, maxJitterSeconds + 1);

        if (jitterSeconds > 0)
        {
            _logger.LogDebug("Waiting {Jitter}s before searching", jitterSeconds);
            await Task.Delay(TimeSpan.FromSeconds(jitterSeconds), _timeProvider);
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
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, arrInstance.ExternalUrl ?? arrInstance.Url);

        try
        {
            IArrClient arrClient = _arrClientFactory.GetClient(item.ArrInstance.ArrConfig.Type, arrInstance.Version);
            HashSet<SearchItem> searchItems = BuildSearchItems(item);

            List<long> commandIds = await arrClient.SearchItemsAsync(arrInstance, searchItems);

            Guid eventId = await _eventPublisher.PublishSearchTriggered(arrInstance.Name, 1, [item.Title], SeekerSearchType.Replacement);

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
            // Round-robin: pick the instance with the oldest LastProcessedAt
            SeekerInstanceConfig nextInstance = instanceConfigs
                .OrderBy(s => s.LastProcessedAt ?? DateTime.MinValue)
                .First();

            await ProcessSingleInstanceAsync(config, nextInstance, isDryRun);
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

    private async Task ProcessSingleInstanceAsync(SeekerConfig config, SeekerInstanceConfig instanceConfig, bool isDryRun)
    {
        ArrInstance arrInstance = instanceConfig.ArrInstance;
        InstanceType instanceType = arrInstance.ArrConfig.Type;

        _logger.LogDebug("Processing {InstanceType} instance: {InstanceName}",
            instanceType, arrInstance.Name);

        // Set context for event publishing
        ContextProvider.Set(nameof(InstanceType), instanceType);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, arrInstance.ExternalUrl ?? arrInstance.Url);

        if (instanceConfig.ActiveDownloadLimit > 0)
        {
            try
            {
                IArrClient arrClient = _arrClientFactory.GetClient(instanceType, arrInstance.Version);
                int activeDownloads = await arrClient.GetActiveDownloadCountAsync(arrInstance);
                if (activeDownloads >= instanceConfig.ActiveDownloadLimit)
                {
                    _logger.LogInformation(
                        "Skipping proactive search for {InstanceName} — {Count} items actively downloading (limit: {Limit})",
                        arrInstance.Name, activeDownloads, instanceConfig.ActiveDownloadLimit);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check active downloads for {InstanceName}, proceeding anyway",
                    arrInstance.Name);
            }
        }

        try
        {
            await ProcessInstanceAsync(config, instanceConfig, arrInstance, instanceType, isDryRun);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {InstanceType} instance: {InstanceName}",
                instanceType, arrInstance.Name);
        }

        if (!isDryRun)
        {
            // Update LastProcessedAt so round-robin moves on
            instanceConfig.LastProcessedAt = DateTime.UtcNow;
            _dataContext.SeekerInstanceConfigs.Update(instanceConfig);
            await _dataContext.SaveChangesAsync();
        }
    }

    private async Task ProcessInstanceAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        InstanceType instanceType,
        bool isDryRun)
    {
        // Load search history for the current cycle
        List<SeekerHistory> currentCycleHistory = await _dataContext.SeekerHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == arrInstance.Id && h.RunId == instanceConfig.CurrentRunId)
            .ToListAsync();

        // Load all history for stale cleanup
        List<SeekerHistory> allHistory = await _dataContext.SeekerHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == arrInstance.Id)
            .ToListAsync();

        // Derive item-level history for selection strategies
        Dictionary<long, DateTime> itemSearchHistory = currentCycleHistory
            .GroupBy(h => h.ExternalItemId)
            .ToDictionary(g => g.Key, g => g.Max(h => h.LastSearchedAt));

        HashSet<SearchItem> searchItems;
        List<string> selectedNames;
        List<long> allLibraryIds;
        List<long> historyIds;
        int seasonNumber = 0;

        if (instanceType == InstanceType.Radarr)
        {
            List<long> selectedIds;
            (selectedIds, selectedNames, allLibraryIds) = await ProcessRadarrAsync(config, arrInstance, instanceConfig, itemSearchHistory, isDryRun);
            searchItems = selectedIds.Select(id => new SearchItem { Id = id }).ToHashSet();
            historyIds = selectedIds;
        }
        else
        {
            (searchItems, selectedNames, allLibraryIds, historyIds, seasonNumber) =
                await ProcessSonarrAsync(config, arrInstance, instanceConfig, itemSearchHistory, currentCycleHistory, isDryRun);
        }

        IEnumerable<long> historyExternalIds = allHistory.Select(h => h.ExternalItemId);

        if (searchItems.Count == 0)
        {
            _logger.LogDebug("No items selected for search on {InstanceName}", arrInstance.Name);
            if (!isDryRun)
            {
                await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, allLibraryIds, historyExternalIds);
            }
            return;
        }

        // Trigger search (arr client guards the HTTP request via dry run interceptor)
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType, arrInstance.Version);
        List<long> commandIds = await arrClient.SearchItemsAsync(arrInstance, searchItems);

        // Publish event (always saved, flagged with IsDryRun in EventPublisher)
        Guid eventId = await _eventPublisher.PublishSearchTriggered(arrInstance.Name, searchItems.Count, selectedNames, SeekerSearchType.Proactive, instanceConfig.CurrentRunId);

        _logger.LogInformation("Searched {Count} items on {InstanceName}: {Items}",
            searchItems.Count, arrInstance.Name, string.Join(", ", selectedNames));

        if (!isDryRun)
        {
            // Update search history
            await UpdateSearchHistoryAsync(arrInstance.Id, instanceType, instanceConfig.CurrentRunId, historyIds, selectedNames, seasonNumber);

            // Track commands
            long externalItemId = historyIds.FirstOrDefault();
            string itemTitle = selectedNames.FirstOrDefault() ?? string.Empty;
            await SaveCommandTrackersAsync(commandIds, eventId, arrInstance.Id, instanceType, externalItemId, itemTitle, seasonNumber);

            // Cleanup stale history entries and old cycle history
            await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, allLibraryIds, historyExternalIds);
            await CleanupOldCycleHistoryAsync(arrInstance.Id, instanceConfig.CurrentRunId);
        }
    }

    private async Task<(List<long> SelectedIds, List<string> SelectedNames, List<long> AllLibraryIds)> ProcessRadarrAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        SeekerInstanceConfig instanceConfig,
        Dictionary<long, DateTime> searchHistory,
        bool isDryRun)
    {
        List<SearchableMovie> movies = await _radarrClient.GetAllMoviesAsync(arrInstance);
        List<long> allLibraryIds = movies.Select(m => m.Id).ToList();

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
        // Items without a cached CF score pass the CF filter.
        var candidates = movies
            .Where(m => m.Status is "released")
            .Where(m => !config.MonitoredOnly || m.Monitored)
            .Where(m => instanceConfig.SkipTags.Count == 0 || !m.Tags.Any(instanceConfig.SkipTags.Contains))
            .Where(m => !m.HasFile
                || (!config.UseCutoff && !config.UseCustomFormatScore)
                || (config.UseCutoff && (m.MovieFile?.QualityCutoffNotMet ?? true))
                || (config.UseCustomFormatScore && (cfScores == null || !cfScores.TryGetValue(m.Id, out var entry) || entry.CurrentScore < entry.CutoffScore)))
            .ToList();

        instanceConfig.TotalEligibleItems = candidates.Count;

        if (candidates.Count == 0)
        {
            return ([], [], allLibraryIds);
        }

        // Check for cycle completion: all candidates already searched in current cycle
        bool cycleComplete = candidates.All(m => searchHistory.ContainsKey(m.Id));
        if (cycleComplete)
        {
            _logger.LogInformation("All {Count} items on {InstanceName} searched in current cycle, starting new cycle",
                candidates.Count, arrInstance.Name);
            if (!isDryRun)
            {
                instanceConfig.CurrentRunId = Guid.NewGuid();
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

        return (selectedIds, selectedNames, allLibraryIds);
    }

    private async Task<(HashSet<SearchItem> SearchItems, List<string> SelectedNames, List<long> AllLibraryIds, List<long> HistoryIds, int SeasonNumber)> ProcessSonarrAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        SeekerInstanceConfig instanceConfig,
        Dictionary<long, DateTime> seriesSearchHistory,
        List<SeekerHistory> currentCycleHistory,
        bool isDryRun)
    {
        List<SearchableSeries> series = await _sonarrClient.GetAllSeriesAsync(arrInstance);
        List<long> allLibraryIds = series.Select(s => s.Id).ToList();

        // Apply filters
        var candidates = series
            .Where(s => s.Status is "continuing" or "ended" or "released")
            .Where(s => !config.MonitoredOnly || s.Monitored)
            .Where(s => instanceConfig.SkipTags.Count == 0 || !s.Tags.Any(instanceConfig.SkipTags.Contains))
            // Skip fully-downloaded series (unless quality upgrade filters active)
            .Where(s => config.UseCutoff || config.UseCustomFormatScore
                || s.Statistics == null || s.Statistics.EpisodeCount == 0
                || s.Statistics.EpisodeFileCount < s.Statistics.EpisodeCount)
            .ToList();

        instanceConfig.TotalEligibleItems = candidates.Count;

        if (candidates.Count == 0)
        {
            return ([], [], allLibraryIds, [], 0);
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
            try
            {
                List<SeekerHistory> seriesHistory = currentCycleHistory
                    .Where(h => h.ExternalItemId == seriesId)
                    .ToList();

                (SeriesSearchItem? searchItem, SearchableEpisode? selectedEpisode) =
                    await BuildSonarrSearchItemAsync(config, arrInstance, seriesId, seriesHistory);

                if (searchItem is not null)
                {
                    string seriesTitle = candidates.First(s => s.Id == seriesId).Title;
                    string displayName = $"{seriesTitle} S{searchItem.Id:D2}";
                    int seasonNumber = (int)searchItem.Id;

                    return ([searchItem], [displayName], allLibraryIds, [seriesId], seasonNumber);
                }

                _logger.LogDebug("Skipping series {SeriesId} — no qualifying seasons found", seriesId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check episodes for series {SeriesId}, skipping", seriesId);
            }
        }

        // All candidates were tried and none had qualifying unsearched seasons — cycle complete
        if (candidates.Count > 0)
        {
            _logger.LogInformation("All series/seasons on {InstanceName} searched in current cycle, starting new cycle",
                arrInstance.Name);
            if (!isDryRun)
            {
                instanceConfig.CurrentRunId = Guid.NewGuid();
                _dataContext.SeekerInstanceConfigs.Update(instanceConfig);
                await _dataContext.SaveChangesAsync();
            }

            // Retry with fresh cycle
            return await ProcessSonarrAsync(config, arrInstance, instanceConfig,
                new Dictionary<long, DateTime>(), [], isDryRun);
        }

        return ([], [], allLibraryIds, [], 0);
    }

    /// <summary>
    /// Fetches episodes for a series and builds a season-level search item.
    /// Uses search history to prefer least-recently-searched seasons.
    /// </summary>
    private async Task<(SeriesSearchItem? SearchItem, SearchableEpisode? SelectedEpisode)> BuildSonarrSearchItemAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        long seriesId,
        List<SeekerHistory> seriesHistory)
    {
        List<SearchableEpisode> episodes = await _sonarrClient.GetEpisodesAsync(arrInstance, seriesId);

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

        // Filter to qualifying episodes — UseCutoff and UseCustomFormatScore are OR-ed
        var qualifying = episodes
            .Where(e => e.AirDateUtc.HasValue && e.AirDateUtc.Value <= DateTime.UtcNow)
            .Where(e => !config.MonitoredOnly || e.Monitored)
            .Where(e => !e.HasFile
                || (!config.UseCutoff && !config.UseCustomFormatScore)
                || (config.UseCutoff && (e.EpisodeFile?.QualityCutoffNotMet ?? true))
                || (config.UseCustomFormatScore && (cfScores == null || !cfScores.TryGetValue(e.Id, out var entry) || entry.CurrentScore < entry.CutoffScore)))
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToList();

        if (qualifying.Count == 0)
        {
            return (null, null);
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
        if (unsearched.Count == 0)
        {
            // All seasons searched in current cycle — this series is done
            return (null, null);
        }

        // Pick from unsearched seasons with some randomization
        var selected = unsearched
            .OrderBy(_ => Random.Shared.Next())
            .First();

        SeriesSearchItem searchItem = new()
        {
            Id = selected.SeasonNumber,
            SeriesId = seriesId,
            SearchType = SeriesSearchType.Season
        };

        return (searchItem, selected.FirstEpisode);
    }

    private async Task UpdateSearchHistoryAsync(
        Guid arrInstanceId,
        InstanceType instanceType,
        Guid runId,
        List<long> searchedIds,
        List<string>? itemTitles = null,
        int seasonNumber = 0)
    {
        var now = DateTime.UtcNow;

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
                    && h.RunId == runId);

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
                    RunId = runId,
                    LastSearchedAt = now,
                    ItemTitle = title,
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
    private async Task CleanupOldCycleHistoryAsync(Guid arrInstanceId, Guid currentRunId)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-30);

        int deleted = await _dataContext.SeekerHistory
            .Where(h => h.ArrInstanceId == arrInstanceId
                && h.RunId != currentRunId
                && h.LastSearchedAt < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogDebug("Cleaned up {Count} old cycle history entries (>30 days) for instance {InstanceId}",
                deleted, arrInstanceId);
        }
    }
}
