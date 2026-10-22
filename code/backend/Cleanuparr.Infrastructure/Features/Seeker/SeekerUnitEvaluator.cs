using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// Works out which units an arr instance needs searched. It only reads, so the Seeker job and the preview share it.
/// </summary>
public sealed class SeekerUnitEvaluator
{
    /// <summary>
    /// Queue states that indicate an item is actively being processed.
    /// Items in these states are excluded from proactive searches.
    /// "importFailed" is left out on purpose: failed imports should be re-searched.
    /// </summary>
    private static readonly HashSet<string> ActiveQueueStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "downloading",
        "importing",
        "importPending",
        "importBlocked"
    };

    private readonly ILogger<SeekerUnitEvaluator> _logger;
    private readonly EventsContext _eventsContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly TimeProvider _timeProvider;

    public SeekerUnitEvaluator(
        ILogger<SeekerUnitEvaluator> logger,
        EventsContext eventsContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _eventsContext = eventsContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Reads what the arr grabbed within the instance's recent grab window. Empty when the window is off.
    /// </summary>
    public async Task<SeekerRecentGrabs> ReadRecentGrabsAsync(
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        CancellationToken cancellationToken)
    {
        if (instanceConfig.RecentGrabWindowHours <= 0)
        {
            return new SeekerRecentGrabs();
        }

        DateTimeOffset since = _timeProvider.GetUtcNow().AddHours(-instanceConfig.RecentGrabWindowHours);

        if (arrInstance.ArrConfig.Type == InstanceType.Radarr)
        {
            return new SeekerRecentGrabs
            {
                MovieIds = await _radarrClient.GetRecentlyGrabbedMovieIdsAsync(arrInstance, since, cancellationToken)
            };
        }

        return new SeekerRecentGrabs
        {
            Episodes = await _sonarrClient.GetRecentlyGrabbedEpisodesAsync(arrInstance, since, cancellationToken)
        };
    }

    /// <summary>
    /// Runs the library through every filter and builds the units that need a search.
    /// </summary>
    public async Task<SeekerEvaluation> EvaluateAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        List<QueueRecord> queueRecords,
        SeekerRecentGrabs recentGrabs,
        List<SeekerHistory> currentCycleHistory,
        CancellationToken cancellationToken)
    {
        List<QueueRecord> activeQueueRecords = queueRecords
            .Where(r => ActiveQueueStates.Contains(r.TrackedDownloadState))
            .ToList();

        Dictionary<long, DateTimeOffset> lastSearchedByItem = currentCycleHistory
            .GroupBy(h => h.ExternalItemId)
            .ToDictionary(g => g.Key, g => g.Max(h => h.LastSearchedAt));

        if (arrInstance.ArrConfig.Type == InstanceType.Radarr)
        {
            return await EvaluateRadarrAsync(config, instanceConfig, arrInstance, activeQueueRecords,
                recentGrabs.MovieIds, lastSearchedByItem, cancellationToken);
        }

        return await EvaluateSonarrAsync(config, instanceConfig, arrInstance, activeQueueRecords,
            recentGrabs.Episodes, currentCycleHistory, lastSearchedByItem, cancellationToken);
    }

    /// <summary>
    /// Picks up to <paramref name="count"/> unsearched units, at most one per movie or series.
    /// </summary>
    public static List<SeekerSearchCandidate> SelectNext(SeekerEvaluation evaluation, SelectionStrategy strategy, int count)
    {
        Dictionary<long, List<SeekerSearchUnit>> remainingByItem = evaluation.Remaining
            .GroupBy(u => u.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        (long Id, DateTimeOffset? Added, DateTimeOffset? LastSearched)[] selectionCandidates = BuildSelectionCandidates(evaluation, remainingByItem);

        // The selectors sort stably, so unshuffled ties would favour the arr's lowest ids
        Random.Shared.Shuffle(selectionCandidates);

        return ItemSelectorFactory.Create(strategy)
            .Select([.. selectionCandidates], count)
            .Select(id => ToCandidate(PickUnit(remainingByItem[id])))
            .ToList();
    }

    /// <summary>
    /// Whether a picked item ties with another remaining item on the strategy's sort key, so the job can pick differently.
    /// </summary>
    public static bool HasTiedPicks(SeekerEvaluation evaluation, SelectionStrategy strategy, IReadOnlySet<long> pickedIds)
    {
        Dictionary<long, List<SeekerSearchUnit>> remainingByItem = evaluation.Remaining
            .GroupBy(u => u.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return BuildSelectionCandidates(evaluation, remainingByItem)
            .GroupBy(c => strategy == SelectionStrategy.NewestFirst ? c.Added : c.LastSearched)
            .Any(g => g.Count() > 1 && g.Any(c => pickedIds.Contains(c.Id)));
    }

    private static (long Id, DateTimeOffset? Added, DateTimeOffset? LastSearched)[] BuildSelectionCandidates(
        SeekerEvaluation evaluation,
        Dictionary<long, List<SeekerSearchUnit>> remainingByItem)
    {
        Dictionary<long, DateTimeOffset?> arrSearchedByItem = evaluation.Units
            .GroupBy(u => u.ItemId)
            .ToDictionary(g => g.Key, g => g.Max(u => u.LastSearchTime));

        return remainingByItem
            .Select(p =>
            {
                DateTimeOffset? cycleSearched = evaluation.LastSearchedByItem.TryGetValue(p.Key, out DateTimeOffset at) ? at : null;
                DateTimeOffset? lastSearched = new[] { arrSearchedByItem[p.Key], cycleSearched }.Max();
                return (p.Key, p.Value[0].Added, lastSearched);
            })
            .ToArray();
    }

    private async Task<SeekerEvaluation> EvaluateRadarrAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        List<QueueRecord> activeQueueRecords,
        HashSet<long> recentlyGrabbedMovieIds,
        Dictionary<long, DateTimeOffset> lastSearchedByItem,
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
                .ToDictionaryAsync(e => e.ExternalItemId, cancellationToken);

            List<ArrQualityProfile> profiles = await _radarrClient.GetQualityProfilesAsync(arrInstance);
            upgradesDisabledProfileIds = profiles.Where(p => !p.UpgradeAllowed).Select(p => p.Id).ToHashSet();
        }

        DateTimeOffset graceCutoff = _timeProvider.GetUtcNow().AddHours(-config.PostReleaseGraceHours);

        List<long> allLibraryIds = new();
        List<SeekerExclusion> exclusions = [];
        int releasedCount = 0;
        int monitoredCount = 0;
        List<SearchableMovie> released = new();
        await foreach (SearchableMovie movie in _radarrClient.StreamAllMoviesAsync(arrInstance, cancellationToken))
        {
            allLibraryIds.Add(movie.Id);

            if (movie.Status is not "released")
            {
                exclusions.Add(new SeekerExclusion(movie.Title, SeekerExclusionReason.NotReleased));
                continue;
            }

            if (!IsMoviePastGracePeriod(movie, graceCutoff))
            {
                exclusions.Add(new SeekerExclusion(movie.Title, SeekerExclusionReason.InGracePeriod));
                continue;
            }

            releasedCount++;

            if (instanceConfig.MonitoredOnly && !movie.Monitored)
            {
                exclusions.Add(new SeekerExclusion(movie.Title, SeekerExclusionReason.NotMonitored));
                continue;
            }

            monitoredCount++;

            SeekerExclusionReason? tagExclusion = GetTagExclusion(movie.Tags, tagsById, skipTagSet, includeTagSet);
            if (tagExclusion is not null)
            {
                exclusions.Add(new SeekerExclusion(movie.Title, tagExclusion.Value));
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

            SeekerEvaluation Unsupported() => new()
            {
                AllLibraryIds = allLibraryIds,
                WantedUnsupported = true,
                ReleasedItems = releasedCount,
                MonitoredItems = monitoredCount,
                TagFilteredItems = released.Count,
                ItemExclusions = exclusions,
                Units = [],
                LastSearchedByItem = lastSearchedByItem,
                WithoutCycleHistory = Unsupported,
            };

            return Unsupported();
        }

        HashSet<long> queuedMovieIds = activeQueueRecords
            .Where(r => r.MovieId > 0)
            .Select(r => r.MovieId)
            .ToHashSet();

        List<SeekerSearchUnit> units = [];
        foreach (SearchableMovie movie in released)
        {
            CustomFormatScoreEntry? entry = cfScores?.GetValueOrDefault(movie.Id);
            bool belowCustomFormatCutoff = entry is not null && entry.CurrentScore < entry.CutoffScore;
            bool upgradesDisabled = belowCustomFormatCutoff && upgradesDisabledProfileIds.Contains(movie.QualityProfileId);

            List<SeekerSearchReason> reasons = [];
            if (missingIds.Contains(movie.Id))
            {
                reasons.Add(SeekerSearchReason.Missing);
            }

            if (cutoffUnmetIds.Contains(movie.Id))
            {
                reasons.Add(SeekerSearchReason.QualityCutoffNotMet);
            }

            int customFormatGap = 0;
            if (belowCustomFormatCutoff && !upgradesDisabled)
            {
                reasons.Add(SeekerSearchReason.CustomFormatScoreBelowCutoff);
                customFormatGap = entry!.CutoffScore - entry.CurrentScore;
            }

            if (reasons.Count == 0)
            {
                if (upgradesDisabled)
                {
                    _logger.LogDebug("skip | upgrades disabled in profile | {Title} | {InstanceName}", movie.Title, arrInstance.Name);
                }

                exclusions.Add(new SeekerExclusion(movie.Title,
                    upgradesDisabled ? SeekerExclusionReason.UpgradesDisabled : SeekerExclusionReason.NothingToSearch));
                continue;
            }

            SeekerExclusionReason? blockedBy = null;
            if (queuedMovieIds.Contains(movie.Id))
            {
                blockedBy = SeekerExclusionReason.InQueue;
            }
            else if (recentlyGrabbedMovieIds.Contains(movie.Id))
            {
                blockedBy = SeekerExclusionReason.RecentlyGrabbed;
            }

            units.Add(new SeekerSearchUnit
            {
                ItemId = movie.Id,
                SeasonNumber = 0,
                EpisodeId = 0,
                Name = movie.Title,
                Reasons = reasons,
                Added = movie.Added,
                LastSearchTime = movie.LastSearchTime,
                CustomFormatGap = customFormatGap,
                Searched = false,
                BlockedBy = blockedBy,
            });
        }

        SeekerEvaluation Build(Dictionary<long, DateTimeOffset> searched) => new()
        {
            AllLibraryIds = allLibraryIds,
            ReleasedItems = releasedCount,
            MonitoredItems = monitoredCount,
            TagFilteredItems = released.Count,
            ItemExclusions = exclusions,
            Units = units.Select(u => u with { Searched = searched.ContainsKey(u.ItemId) }).ToList(),
            LastSearchedByItem = searched,
            WithoutCycleHistory = () => Build(new Dictionary<long, DateTimeOffset>()),
        };

        return Build(lastSearchedByItem);
    }

    private async Task<SeekerEvaluation> EvaluateSonarrAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        List<QueueRecord> activeQueueRecords,
        HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)> recentlyGrabbedEpisodes,
        List<SeekerHistory> currentCycleHistory,
        Dictionary<long, DateTimeOffset> lastSearchedByItem,
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
        List<SeekerExclusion> exclusions = [];
        int releasedCount = 0;
        int monitoredCount = 0;
        Dictionary<long, SearchableSeries> seriesById = new();
        await foreach (SearchableSeries series in _sonarrClient.StreamAllSeriesAsync(arrInstance, cancellationToken))
        {
            allLibraryIds.Add(series.Id);

            if (series.Status is not ("continuing" or "ended" or "released"))
            {
                exclusions.Add(new SeekerExclusion(series.Title, SeekerExclusionReason.NotReleased));
                continue;
            }

            releasedCount++;

            if (instanceConfig.MonitoredOnly && !series.Monitored)
            {
                exclusions.Add(new SeekerExclusion(series.Title, SeekerExclusionReason.NotMonitored));
                continue;
            }

            monitoredCount++;

            SeekerExclusionReason? tagExclusion = GetTagExclusion(series.Tags, tagsById, skipTagSet, includeTagSet);
            if (tagExclusion is not null)
            {
                exclusions.Add(new SeekerExclusion(series.Title, tagExclusion.Value));
                continue;
            }

            seriesById[series.Id] = series;
        }

        List<SonarrWantedEpisode> wanted = [];
        DateTimeOffset now = _timeProvider.GetUtcNow();
        HashSet<long> excludedEpisodeIds = [];
        HashSet<long> seriesWithExcludedEpisodes = [];

        void ExcludeEpisode(long seriesId, long episodeId, string name, SeekerExclusionReason reason)
        {
            seriesWithExcludedEpisodes.Add(seriesId);
            if (excludedEpisodeIds.Add(episodeId))
            {
                exclusions.Add(new SeekerExclusion(name, reason));
            }
        }

        bool IsReleasedEpisodeOfCandidate(SearchableEpisode e)
        {
            if (!seriesById.TryGetValue(e.SeriesId, out SearchableSeries? series))
            {
                return false;
            }

            if (e.AirDateUtc.HasValue && e.AirDateUtc.Value <= graceCutoff)
            {
                return true;
            }

            ExcludeEpisode(e.SeriesId, e.Id, $"{series.Title} S{e.SeasonNumber:D2}E{e.EpisodeNumber:D2}",
                e.AirDateUtc is null || e.AirDateUtc.Value > now
                    ? SeekerExclusionReason.NotReleased
                    : SeekerExclusionReason.InGracePeriod);
            return false;
        }

        SonarrWantedEpisode ToWanted(SearchableEpisode e, SeekerSearchReason reason) => new()
        {
            SeriesId = e.SeriesId,
            SeasonNumber = e.SeasonNumber,
            EpisodeId = e.Id,
            EpisodeNumber = e.EpisodeNumber,
            AirDateUtc = e.AirDateUtc,
            LastSearchTime = e.LastSearchTime,
            Monitored = e.Monitored,
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

            HashSet<long> wantedEpisodeIds = wanted.Select(w => w.EpisodeId).ToHashSet();

            foreach (CustomFormatScoreEntry entry in belowCutoff)
            {
                if (!seriesById.TryGetValue(entry.ExternalItemId, out SearchableSeries? series))
                {
                    continue;
                }

                if (upgradesDisabledProfileIds.Contains(series.QualityProfileId))
                {
                    if (!wantedEpisodeIds.Contains(entry.EpisodeId))
                    {
                        _logger.LogDebug("skip | upgrades disabled in profile | {Title} | {InstanceName}", entry.Title, arrInstance.Name);
                        ExcludeEpisode(entry.ExternalItemId, entry.EpisodeId, entry.Title, SeekerExclusionReason.UpgradesDisabled);
                    }

                    continue;
                }

                wanted.Add(new SonarrWantedEpisode
                {
                    SeriesId = entry.ExternalItemId,
                    SeasonNumber = entry.SeasonNumber!.Value,
                    EpisodeId = entry.EpisodeId,
                    Title = entry.Title,
                    AirDateUtc = entry.AirDateUtc,
                    LastSearchTime = entry.LastSearchTime,
                    Monitored = entry.IsMonitored,
                    CustomFormatGap = entry.CutoffScore - entry.CurrentScore,
                    Reason = SeekerSearchReason.CustomFormatScoreBelowCutoff,
                });
            }
        }

        HashSet<(long SeriesId, int SeasonNumber)> finishedAnimeSeasons = await GetFinishedAnimeSeasonsAsync(
            arrInstance, seriesById, wanted, cancellationToken);

        HashSet<long> wantedSeriesIds = wanted.Select(w => w.SeriesId).ToHashSet();
        exclusions.AddRange(seriesById.Values
            .Where(s => !wantedSeriesIds.Contains(s.Id) && !seriesWithExcludedEpisodes.Contains(s.Id))
            .Select(s => new SeekerExclusion(s.Title, SeekerExclusionReason.NothingToSearch)));

        HashSet<(long SeriesId, long SeasonNumber)> queuedSeasons = activeQueueRecords
            .Where(r => r.SeriesId > 0)
            .Select(r => (r.SeriesId, r.SeasonNumber))
            .ToHashSet();
        HashSet<long> queuedEpisodeIds = activeQueueRecords
            .Where(r => r.EpisodeId > 0)
            .Select(r => r.EpisodeId)
            .ToHashSet();
        HashSet<(long SeriesId, long SeasonNumber)> grabbedSeasons = recentlyGrabbedEpisodes
            .Select(e => (e.SeriesId, e.SeasonNumber))
            .ToHashSet();
        HashSet<long> grabbedEpisodeIds = recentlyGrabbedEpisodes
            .Select(e => e.EpisodeId)
            .ToHashSet();

        SeekerExclusionReason? GetBlockedBy(SeekerSearchUnit unit)
        {
            bool queued = unit.EpisodeId == 0
                ? queuedSeasons.Contains((unit.ItemId, unit.SeasonNumber))
                : queuedEpisodeIds.Contains(unit.EpisodeId);
            if (queued)
            {
                return SeekerExclusionReason.InQueue;
            }

            bool grabbed = unit.EpisodeId == 0
                ? grabbedSeasons.Contains((unit.ItemId, unit.SeasonNumber))
                : grabbedEpisodeIds.Contains(unit.EpisodeId);
            return grabbed ? SeekerExclusionReason.RecentlyGrabbed : null;
        }

        SeekerEvaluation Build(List<SeekerHistory> history, Dictionary<long, DateTimeOffset> searched)
        {
            HashSet<(long SeriesId, int SeasonNumber)> searchedSeasons = history
                .Where(h => h.EpisodeId == 0 && h.SeasonNumber > 0)
                .Select(h => (h.ExternalItemId, h.SeasonNumber))
                .ToHashSet();
            HashSet<long> searchedEpisodeIds = history
                .Where(h => h.EpisodeId > 0)
                .Select(h => h.EpisodeId)
                .ToHashSet();

            return new SeekerEvaluation
            {
                AllLibraryIds = allLibraryIds,
                ReleasedItems = releasedCount,
                MonitoredItems = monitoredCount,
                TagFilteredItems = seriesById.Count,
                ItemExclusions = exclusions,
                Units = BuildSonarrUnits(wanted, seriesById, finishedAnimeSeasons, searchedSeasons, searchedEpisodeIds, instanceConfig.SonarrSearchMode)
                    .Select(u => u with { BlockedBy = GetBlockedBy(u) })
                    .ToList(),
                LastSearchedByItem = searched,
                WithoutCycleHistory = () => Build([], new Dictionary<long, DateTimeOffset>()),
            };
        }

        return Build(currentCycleHistory, lastSearchedByItem);
    }

    /// <summary>
    /// Groups wanted episodes into season and episode units.
    /// Sonarr runs one query per episode for a specials or airing anime season search, so those get episode units.
    /// The search mode decides the unit for standard and daily seasons.
    /// A season partly searched in this cycle continues by episode.
    /// </summary>
    private static List<SeekerSearchUnit> BuildSonarrUnits(
        List<SonarrWantedEpisode> wanted,
        Dictionary<long, SearchableSeries> seriesById,
        HashSet<(long SeriesId, int SeasonNumber)> finishedAnimeSeasons,
        HashSet<(long SeriesId, int SeasonNumber)> searchedSeasons,
        HashSet<long> searchedEpisodeIds,
        SonarrSearchMode mode)
    {
        List<SeekerSearchUnit> units = [];

        foreach (IGrouping<(long SeriesId, int SeasonNumber), SonarrWantedEpisode> season in wanted.GroupBy(w => (w.SeriesId, w.SeasonNumber)))
        {
            SearchableSeries series = seriesById[season.Key.SeriesId];
            SeekerContentKind kind = GetContentKind(series, season.Key.SeasonNumber);
            bool seasonSearched = searchedSeasons.Contains(season.Key);
            bool canSearchSeason = kind switch
            {
                SeekerContentKind.Special => false,
                SeekerContentKind.Anime => finishedAnimeSeasons.Contains(season.Key),
                _ => mode switch
                {
                    SonarrSearchMode.Season => true,
                    SonarrSearchMode.Episode => false,
                    _ => IsFullSeasonSearch(series, season),
                },
            };

            if (canSearchSeason && (seasonSearched || !season.Any(w => searchedEpisodeIds.Contains(w.EpisodeId))))
            {
                units.Add(new SeekerSearchUnit
                {
                    ItemId = season.Key.SeriesId,
                    SeasonNumber = season.Key.SeasonNumber,
                    EpisodeId = 0,
                    Name = $"{series.Title} S{season.Key.SeasonNumber:D2}",
                    ContentKind = kind,
                    Reasons = season.Select(w => w.Reason).ToList(),
                    Added = series.Added,
                    LastSearchTime = season.Max(w => w.LastSearchTime),
                    CustomFormatGap = season.Max(w => w.CustomFormatGap),
                    Searched = seasonSearched,
                });
                continue;
            }

            foreach (IGrouping<long, SonarrWantedEpisode> episode in season.GroupBy(w => w.EpisodeId))
            {
                SonarrWantedEpisode first = episode.First();
                int? episodeNumber = episode.Select(w => w.EpisodeNumber).FirstOrDefault(n => n is not null);

                units.Add(new SeekerSearchUnit
                {
                    ItemId = season.Key.SeriesId,
                    SeasonNumber = season.Key.SeasonNumber,
                    EpisodeId = episode.Key,
                    Name = episodeNumber is not null
                        ? $"{series.Title} S{season.Key.SeasonNumber:D2}E{episodeNumber:D2}"
                        : first.Title ?? $"{series.Title} S{season.Key.SeasonNumber:D2}",
                    ContentKind = kind,
                    Reasons = episode.Select(w => w.Reason).ToList(),
                    AirDateUtc = first.AirDateUtc,
                    Added = series.Added,
                    LastSearchTime = episode.Max(w => w.LastSearchTime),
                    CustomFormatGap = episode.Max(w => w.CustomFormatGap),
                    Searched = seasonSearched || searchedEpisodeIds.Contains(episode.Key),
                });
            }
        }

        return units;
    }

    /// <summary>
    /// Every aired monitored episode of a finished season needs a search, read from Sonarr's season statistics.
    /// </summary>
    private static bool IsFullSeasonSearch(SearchableSeries series, IGrouping<(long SeriesId, int SeasonNumber), SonarrWantedEpisode> season)
    {
        SeasonStatistics? stats = series.Seasons.FirstOrDefault(s => s.SeasonNumber == season.Key.SeasonNumber)?.Statistics;

        if (stats is null || stats.NextAiring is not null || season.Any(w => !w.Monitored))
        {
            return false;
        }

        // EpisodeCount holds the monitored aired episodes plus any episode with a file
        return season.Select(w => w.EpisodeId).Distinct().Count() == stats.EpisodeCount;
    }

    /// <summary>
    /// Picks a random season of the item, then its earliest airing unit.
    /// </summary>
    private static SeekerSearchUnit PickUnit(List<SeekerSearchUnit> itemUnits)
    {
        List<int> seasons = itemUnits.Select(u => u.SeasonNumber).Distinct().ToList();
        int seasonNumber = seasons[Random.Shared.Next(seasons.Count)];

        return itemUnits
            .Where(u => u.SeasonNumber == seasonNumber)
            .OrderBy(u => u.AirDateUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(u => u.EpisodeId)
            .First();
    }

    private static SeekerSearchCandidate ToCandidate(SeekerSearchUnit unit) => new()
    {
        ItemId = unit.ItemId,
        Name = unit.Name,
        SeasonNumber = unit.SeasonNumber,
        EpisodeId = unit.EpisodeId,
        ContentKind = unit.ContentKind,
        Reason = PrimaryReason(unit.Reasons),
    };

    private static SeekerSearchReason PrimaryReason(List<SeekerSearchReason> reasons)
    {
        if (reasons.Contains(SeekerSearchReason.Missing))
        {
            return SeekerSearchReason.Missing;
        }

        if (reasons.Contains(SeekerSearchReason.QualityCutoffNotMet))
        {
            return SeekerSearchReason.QualityCutoffNotMet;
        }

        return SeekerSearchReason.CustomFormatScoreBelowCutoff;
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

    /// <summary>
    /// Returns true when the movie's release date is past the grace period cutoff.
    /// Movies without any release date info are treated as released.
    /// </summary>
    private static bool IsMoviePastGracePeriod(SearchableMovie movie, DateTimeOffset graceCutoff)
    {
        DateTimeOffset? releaseDate = movie.DigitalRelease ?? movie.PhysicalRelease ?? movie.InCinemas;
        return releaseDate is null || releaseDate.Value <= graceCutoff;
    }

    /// <summary>
    /// Returns why the tags keep the item out, or null when they let it through. A skip tag wins over an include tag.
    /// </summary>
    private static SeekerExclusionReason? GetTagExclusion(
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
            return SeekerExclusionReason.SkipTag;
        }

        return includeTags.Count == 0 || labels.Any(includeTags.Contains)
            ? null
            : SeekerExclusionReason.MissingIncludeTag;
    }
}
