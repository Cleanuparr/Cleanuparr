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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

public sealed class Seeker : IHandler
{
    private readonly ILogger<Seeker> _logger;
    private readonly DataContext _dataContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly IArrClientFactory _arrClientFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDryRunInterceptor _dryRunInterceptor;

    public Seeker(
        ILogger<Seeker> logger,
        DataContext dataContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        IArrClientFactory arrClientFactory,
        IEventPublisher eventPublisher,
        IDryRunInterceptor dryRunInterceptor)
    {
        _logger = logger;
        _dataContext = dataContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _arrClientFactory = arrClientFactory;
        _eventPublisher = eventPublisher;
        _dryRunInterceptor = dryRunInterceptor;
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

        // Replacement searches queued after download removal
        SearchQueueItem? replacementItem = await _dataContext.SearchQueue
            .OrderBy(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        if (replacementItem is not null)
        {
            await ProcessReplacementItemAsync(replacementItem);
            return;
        }

        // Missing items and quality upgrades
        if (!config.ProactiveSearchEnabled)
        {
            return;
        }

        await ProcessProactiveSearchAsync(config);
    }

    private async Task ProcessReplacementItemAsync(SearchQueueItem item)
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

            await _dryRunInterceptor.InterceptAsync(arrClient.SearchItemsAsync, arrInstance, searchItems);

            await _eventPublisher.PublishSearchTriggered(arrInstance.Name, 1, [item.Title]);

            _logger.LogInformation("Replacement search triggered for '{Title}' on {InstanceName}",
                item.Title, arrInstance.Name);

            // Update search history for proactive search awareness
            await UpdateSearchHistoryAsync(arrInstance.Id, item.ArrInstance.ArrConfig.Type, [item.ItemId]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process replacement search for '{Title}' on {InstanceName}",
                item.Title, arrInstance.Name);
        }
        finally
        {
            _dataContext.SearchQueue.Remove(item);
            await _dataContext.SaveChangesAsync();
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

    private async Task ProcessProactiveSearchAsync(SeekerConfig config)
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

            await ProcessSingleInstanceAsync(config, nextInstance);
        }
        else
        {
            // Process all enabled instances sequentially
            foreach (SeekerInstanceConfig instanceConfig in instanceConfigs)
            {
                await ProcessSingleInstanceAsync(config, instanceConfig);
            }
        }
    }

    private async Task ProcessSingleInstanceAsync(SeekerConfig config, SeekerInstanceConfig instanceConfig)
    {
        ArrInstance arrInstance = instanceConfig.ArrInstance;
        InstanceType instanceType = arrInstance.ArrConfig.Type;

        _logger.LogInformation("Seeker processing {InstanceType} instance: {InstanceName}",
            instanceType, arrInstance.Name);

        // Set context for event publishing
        ContextProvider.Set(nameof(InstanceType), instanceType);
        ContextProvider.Set(ContextProvider.Keys.ArrInstanceUrl, arrInstance.ExternalUrl ?? arrInstance.Url);

        try
        {
            await ProcessInstanceAsync(config, instanceConfig, arrInstance, instanceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeker failed to process {InstanceType} instance: {InstanceName}",
                instanceType, arrInstance.Name);
        }

        // Always update LastProcessedAt so round-robin moves on
        instanceConfig.LastProcessedAt = DateTime.UtcNow;
        _dataContext.SeekerInstanceConfigs.Update(instanceConfig);
        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessInstanceAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        InstanceType instanceType)
    {
        // Load search history for this instance
        Dictionary<long, DateTime> searchHistory = await _dataContext.SeekerHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == arrInstance.Id)
            .ToDictionaryAsync(h => h.ExternalItemId, h => h.LastSearchedAt);

        // Fetch and filter items, then select based on strategy
        List<long> selectedIds;
        List<string> selectedNames;
        List<long> allLibraryIds;

        if (instanceType == InstanceType.Radarr)
        {
            (selectedIds, selectedNames, allLibraryIds) = await ProcessRadarrAsync(config, arrInstance, instanceConfig.SkipTags, searchHistory);
        }
        else
        {
            (selectedIds, selectedNames, allLibraryIds) = await ProcessSonarrAsync(config, arrInstance, instanceConfig.SkipTags, searchHistory);
        }

        if (selectedIds.Count == 0)
        {
            _logger.LogDebug("No items selected for search on {InstanceName}", arrInstance.Name);
            // Still cleanup stale entries even if no items selected
            await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, allLibraryIds, searchHistory);
            return;
        }

        // Trigger search
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType, arrInstance.Version);
        HashSet<SearchItem> searchItems = selectedIds.Select(id => new SearchItem { Id = id }).ToHashSet();

        await _dryRunInterceptor.InterceptAsync(arrClient.SearchItemsAsync, arrInstance, searchItems);

        // Update search history
        await UpdateSearchHistoryAsync(arrInstance.Id, instanceType, selectedIds);

        // Publish event
        await _eventPublisher.PublishSearchTriggered(arrInstance.Name, selectedIds.Count, selectedNames);

        _logger.LogInformation("Seeker searched {Count} items on {InstanceName}: {Items}",
            selectedIds.Count, arrInstance.Name, string.Join(", ", selectedNames));

        // Cleanup stale history entries
        await CleanupStaleHistoryAsync(arrInstance.Id, instanceType, allLibraryIds, searchHistory);
    }

    private async Task<(List<long> SelectedIds, List<string> SelectedNames, List<long> AllLibraryIds)> ProcessRadarrAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        List<string> skipTags,
        Dictionary<long, DateTime> searchHistory)
    {
        List<SearchableMovie> movies = await _radarrClient.GetAllMoviesAsync(arrInstance);
        List<long> allLibraryIds = movies.Select(m => m.Id).ToList();

        // Apply filters
        var candidates = movies
            .Where(m => !config.MonitoredOnly || m.Monitored)
            .Where(m => skipTags.Count == 0 || !m.Tags.Any(skipTags.Contains))
            .Where(m => !config.UseCutoff || !m.HasFile || (m.MovieFile?.QualityCutoffNotMet ?? true))
            .ToList();

        if (candidates.Count == 0)
        {
            return ([], [], allLibraryIds);
        }

        // Build selection candidates with history
        var selectionCandidates = candidates
            .Select(m => (m.Id, m.Added, LastSearched: searchHistory.TryGetValue(m.Id, out var dt) ? (DateTime?)dt : null))
            .ToList();

        IItemSelector selector = ItemSelectorFactory.Create(config.SelectionStrategy);
        // Only select 1 item to avoid being banned from indexers (might be configurable down the line?)
        List<long> selectedIds = selector.Select(selectionCandidates, 1);

        List<string> selectedNames = candidates
            .Where(m => selectedIds.Contains(m.Id))
            .Select(m => m.Title)
            .ToList();

        return (selectedIds, selectedNames, allLibraryIds);
    }

    private async Task<(List<long> SelectedIds, List<string> SelectedNames, List<long> AllLibraryIds)> ProcessSonarrAsync(
        SeekerConfig config,
        ArrInstance arrInstance,
        List<string> skipTags,
        Dictionary<long, DateTime> searchHistory)
    {
        List<SearchableSeries> series = await _sonarrClient.GetAllSeriesAsync(arrInstance);
        List<long> allLibraryIds = series.Select(s => s.Id).ToList();

        // Apply filters
        var candidates = series
            .Where(s => !config.MonitoredOnly || s.Monitored)
            .Where(s => skipTags.Count == 0 || !s.Tags.Any(skipTags.Contains))
            .ToList();

        if (candidates.Count == 0)
        {
            return ([], [], allLibraryIds);
        }

        // Build selection candidates with history
        var selectionCandidates = candidates
            .Select(s => (s.Id, s.Added, LastSearched: searchHistory.TryGetValue(s.Id, out var dt) ? (DateTime?)dt : null))
            .ToList();

        // Over-select when using cutoff to compensate for filtered-out series
        IItemSelector selector = ItemSelectorFactory.Create(config.SelectionStrategy);
        List<long> selectedIds = selector.Select(selectionCandidates, 10);

        if (config.UseCutoff)
        {
            selectedIds = await FilterByCutoffAsync(arrInstance, selectedIds, 1);
        }
        else
        {
            selectedIds = selectedIds[..1];
        }

        List<string> selectedNames = candidates
            .Where(s => selectedIds.Contains(s.Id))
            .Select(s => s.Title)
            .ToList();

        return (selectedIds, selectedNames, allLibraryIds);
    }

    /// <summary>
    /// Filters series by checking if any episode still needs a quality upgrade.
    /// Returns up to maxCount series that have at least one episode with qualityCutoffNotMet.
    /// </summary>
    private async Task<List<long>> FilterByCutoffAsync(ArrInstance arrInstance, List<long> seriesIds, int maxCount)
    {
        var result = new List<long>();

        foreach (long seriesId in seriesIds)
        {
            if (result.Count >= maxCount)
            {
                break;
            }

            try
            {
                List<SearchableEpisode> episodes = await _sonarrClient.GetEpisodesAsync(arrInstance, seriesId);

                // Keep the series if any episode has cutoff not met (still needs upgrade)
                // or if any episode has no file (missing)
                bool needsWork = episodes.Any(e =>
                    !e.HasFile || (e.EpisodeFile?.QualityCutoffNotMet ?? true));

                if (needsWork)
                {
                    result.Add(seriesId);
                }
                else
                {
                    _logger.LogDebug("Skipping series {SeriesId} — all episodes meet quality cutoff", seriesId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check cutoff for series {SeriesId}, including it anyway", seriesId);
                result.Add(seriesId);
            }
        }

        return result;
    }

    private async Task UpdateSearchHistoryAsync(Guid arrInstanceId, InstanceType instanceType, List<long> searchedIds)
    {
        var now = DateTime.UtcNow;

        foreach (long id in searchedIds)
        {
            SeekerHistory? existing = await _dataContext.SeekerHistory
                .FirstOrDefaultAsync(h => h.ArrInstanceId == arrInstanceId && h.ExternalItemId == id && h.ItemType == instanceType);

            if (existing is not null)
            {
                existing.LastSearchedAt = now;
            }
            else
            {
                _dataContext.SeekerHistory.Add(new SeekerHistory
                {
                    ArrInstanceId = arrInstanceId,
                    ExternalItemId = id,
                    ItemType = instanceType,
                    LastSearchedAt = now,
                });
            }
        }

        await _dataContext.SaveChangesAsync();
    }

    private async Task CleanupStaleHistoryAsync(
        Guid arrInstanceId,
        InstanceType instanceType,
        List<long> currentLibraryIds,
        Dictionary<long, DateTime> searchHistory)
    {
        // Find history entries for items no longer in the library
        var staleIds = searchHistory.Keys
            .Except(currentLibraryIds)
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
}
