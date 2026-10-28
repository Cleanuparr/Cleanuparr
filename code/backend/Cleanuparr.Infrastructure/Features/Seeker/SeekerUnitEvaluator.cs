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

    internal const int MissesBeforeRest = 3;

    internal const int MaxRestCycles = 8;

    private readonly ILogger<SeekerUnitEvaluator> _logger;
    private readonly EventsContext _eventsContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly ILidarrClient _lidarrClient;
    private readonly IWhisparrV2Client _whisparrV2Client;
    private readonly IWhisparrV3Client _whisparrV3Client;
    private readonly TimeProvider _timeProvider;

    public SeekerUnitEvaluator(
        ILogger<SeekerUnitEvaluator> logger,
        EventsContext eventsContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        ILidarrClient lidarrClient,
        IWhisparrV2Client whisparrV2Client,
        IWhisparrV3Client whisparrV3Client,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _eventsContext = eventsContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _lidarrClient = lidarrClient;
        _whisparrV2Client = whisparrV2Client;
        _whisparrV3Client = whisparrV3Client;
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

        if (SeekerApps.SearchesMovies(arrInstance))
        {
            return new SeekerRecentGrabs
            {
                MovieIds = await MovieClient(arrInstance).GetRecentlyGrabbedMovieIdsAsync(arrInstance, since, cancellationToken)
            };
        }

        if (arrInstance.ArrConfig.Type is InstanceType.Lidarr)
        {
            return new SeekerRecentGrabs
            {
                AlbumIds = await _lidarrClient.GetRecentlyGrabbedAlbumIdsAsync(arrInstance, since, cancellationToken)
            };
        }

        return new SeekerRecentGrabs
        {
            Episodes = await SeriesClient(arrInstance).GetRecentlyGrabbedEpisodesAsync(arrInstance, since, cancellationToken)
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

        Dictionary<(long ItemId, int SeasonNumber, long EpisodeId), SeekerUnitRest> rests =
            await ReadRestsAsync(instanceConfig, arrInstance, cancellationToken);

        if (SeekerApps.SearchesMovies(arrInstance))
        {
            return await EvaluateMoviesAsync(config, instanceConfig, arrInstance, activeQueueRecords,
                recentGrabs.MovieIds, rests, lastSearchedByItem, cancellationToken);
        }

        if (arrInstance.ArrConfig.Type is InstanceType.Lidarr)
        {
            return await EvaluateLidarrAsync(config, instanceConfig, arrInstance, activeQueueRecords,
                recentGrabs.AlbumIds, rests, currentCycleHistory, lastSearchedByItem, cancellationToken);
        }

        return await EvaluateSonarrAsync(config, instanceConfig, arrInstance, activeQueueRecords,
            recentGrabs.Episodes, rests, currentCycleHistory, lastSearchedByItem, cancellationToken);
    }

    /// <summary>
    /// The rest a unit is on, or null when it can be searched.
    /// It starts after <see cref="MissesBeforeRest"/> misses in a row and doubles with each further miss, up to <see cref="MaxRestCycles"/> cycles.
    /// </summary>
    /// <param name="cycleStarts">Start times of the instance's live cycles; each one after the last search counts as a cycle passed.</param>
    public static SeekerUnitRest? GetRest(SeekerHistory unit, IReadOnlyList<DateTimeOffset> cycleStarts, int maxRestDays, DateTimeOffset now)
    {
        if (unit.ConsecutiveMisses < MissesBeforeRest)
        {
            return null;
        }

        DateTimeOffset anchor = unit.LastMissAt ?? unit.LastSearchedAt;
        DateTimeOffset until = anchor.AddDays(maxRestDays);
        if (now >= until)
        {
            return null;
        }

        int restCycles = (int)Math.Min(Math.Pow(2, unit.ConsecutiveMisses - MissesBeforeRest), MaxRestCycles);
        int cyclesSince = cycleStarts.Count(s => s > anchor);
        if (cyclesSince > restCycles)
        {
            return null;
        }

        return new SeekerUnitRest(unit.ConsecutiveMisses, restCycles - cyclesSince + 1, until);
    }

    /// <summary>
    /// Reads the live units resting now, keyed by unit. Empty when resting is off.
    /// </summary>
    private async Task<Dictionary<(long ItemId, int SeasonNumber, long EpisodeId), SeekerUnitRest>> ReadRestsAsync(
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        CancellationToken cancellationToken)
    {
        if (!instanceConfig.RestUnitsEnabled)
        {
            return [];
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset restCap = now.AddDays(-instanceConfig.RestMaxDays);

        List<SeekerHistory> missed = await _eventsContext.SeekerHistory
            .AsNoTracking()
            .Where(h => h.ArrInstanceId == arrInstance.Id
                        && !h.IsDryRun
                        && h.ConsecutiveMisses >= MissesBeforeRest
                        && (h.LastMissAt ?? h.LastSearchedAt) > restCap)
            .ToListAsync(cancellationToken);

        if (missed.Count == 0)
        {
            return [];
        }

        DateTimeOffset earliestSearch = missed.Min(h => h.LastMissAt ?? h.LastSearchedAt);
        List<DateTimeOffset> cycleStarts = await _eventsContext.SeekerCycles
            .AsNoTracking()
            .Where(c => c.ArrInstanceId == arrInstance.Id && !c.IsDryRun && c.StartedAt > earliestSearch)
            .Select(c => c.StartedAt)
            .ToListAsync(cancellationToken);

        Dictionary<(long ItemId, int SeasonNumber, long EpisodeId), SeekerUnitRest> rests = [];
        foreach (SeekerHistory unit in missed)
        {
            SeekerUnitRest? rest = GetRest(unit, cycleStarts, instanceConfig.RestMaxDays, now);
            if (rest is not null)
            {
                rests[(unit.ExternalItemId, unit.SeasonNumber, unit.EpisodeId)] = rest;
            }
        }

        return rests;
    }

    /// <summary>
    /// The strategy the instance runs: <see cref="SelectionStrategy.CustomFormatGapWeighted"/> needs custom format scores.
    /// </summary>
    public static SelectionStrategy EffectiveStrategy(SelectionStrategy strategy, SeekerInstanceConfig instanceConfig)
    {
        return strategy is SelectionStrategy.CustomFormatGapWeighted && !instanceConfig.UseCustomFormatScore
            ? SelectionStrategy.BalancedWeighted
            : strategy;
    }

    /// <summary>
    /// Picks up to <paramref name="count"/> unsearched units, at most one per movie, series or artist.
    /// </summary>
    public static List<SeekerSearchCandidate> SelectNext(SeekerEvaluation evaluation, SelectionStrategy strategy, int count)
    {
        Dictionary<long, List<SeekerSearchUnit>> remainingByItem = evaluation.Remaining
            .GroupBy(u => u.ItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        (long Id, DateTimeOffset? Added, DateTimeOffset? LastSearched, double Gap)[] selectionCandidates = BuildSelectionCandidates(evaluation, remainingByItem);

        // The selectors sort stably, so unshuffled ties would favour the arr's lowest ids
        Random.Shared.Shuffle(selectionCandidates);

        return ItemSelectorFactory.Create(strategy)
            .Select([.. selectionCandidates], count)
            .Select(id => ToCandidate(PickUnit(remainingByItem[id], strategy)))
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

    private static (long Id, DateTimeOffset? Added, DateTimeOffset? LastSearched, double Gap)[] BuildSelectionCandidates(
        SeekerEvaluation evaluation,
        Dictionary<long, List<SeekerSearchUnit>> remainingByItem)
    {
        Dictionary<long, DateTimeOffset?> arrSearchedByItem = evaluation.Units
            .GroupBy(u => u.ItemId)
            .ToDictionary(g => g.Key, g => g.Max(u => u.LastSearchTime));

        HashSet<long> missingItemIds = remainingByItem
            .Where(p => p.Value.Any(u => u.Reasons.Contains(SeekerSearchReason.Missing)))
            .Select(p => p.Key)
            .ToHashSet();

        // Missing items rank as furthest below the cutoff
        int missingGap = Math.Max(1, remainingByItem
            .Where(p => !missingItemIds.Contains(p.Key))
            .Select(p => p.Value.Max(u => u.CustomFormatGap))
            .DefaultIfEmpty(0)
            .Max());

        return remainingByItem
            .Select(p =>
            {
                DateTimeOffset? cycleSearched = evaluation.LastSearchedByItem.TryGetValue(p.Key, out DateTimeOffset at) ? at : null;
                DateTimeOffset? lastSearched = new[] { arrSearchedByItem[p.Key], cycleSearched }.Max();
                int gap = missingItemIds.Contains(p.Key) ? missingGap : p.Value.Max(u => u.CustomFormatGap);
                return (p.Key, p.Value[0].Added, lastSearched, (double)gap);
            })
            .ToArray();
    }

    private IRadarrClient MovieClient(ArrInstance arrInstance) =>
        arrInstance.ArrConfig.Type is InstanceType.Whisparr ? _whisparrV3Client : _radarrClient;

    private ISonarrClient SeriesClient(ArrInstance arrInstance) =>
        arrInstance.ArrConfig.Type is InstanceType.Whisparr ? _whisparrV2Client : _sonarrClient;

    private async Task<SeekerEvaluation> EvaluateMoviesAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        List<QueueRecord> activeQueueRecords,
        HashSet<long> recentlyGrabbedMovieIds,
        Dictionary<(long ItemId, int SeasonNumber, long EpisodeId), SeekerUnitRest> rests,
        Dictionary<long, DateTimeOffset> lastSearchedByItem,
        CancellationToken cancellationToken)
    {
        IRadarrClient movieClient = MovieClient(arrInstance);
        InstanceType instanceType = arrInstance.ArrConfig.Type;
        List<Tag> tags = await movieClient.GetAllTagsAsync(arrInstance);
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
                .Where(e => e.ArrInstanceId == arrInstance.Id && e.ItemType == instanceType)
                .ToDictionaryAsync(e => e.ExternalItemId, cancellationToken);

            List<ArrQualityProfile> profiles = await movieClient.GetQualityProfilesAsync(arrInstance);
            upgradesDisabledProfileIds = profiles.Where(p => !p.UpgradeAllowed).Select(p => p.Id).ToHashSet();
        }

        DateTimeOffset graceCutoff = _timeProvider.GetUtcNow().AddHours(-config.PostReleaseGraceHours);
        RadarrReleaseGate releaseGate = instanceType is InstanceType.Radarr
            ? instanceConfig.RadarrReleaseGate
            : RadarrReleaseGate.ReleaseDate;

        List<long> allLibraryIds = new();
        List<SeekerExclusion> exclusions = [];
        int releasedCount = 0;
        int monitoredCount = 0;
        List<SearchableMovie> released = new();
        await foreach (SearchableMovie movie in movieClient.StreamAllMoviesAsync(arrInstance, cancellationToken))
        {
            allLibraryIds.Add(movie.Id);

            SeekerExclusionReason? releaseExclusion = GetReleaseExclusion(movie, releaseGate, graceCutoff);
            if (releaseExclusion is not null)
            {
                exclusions.Add(new SeekerExclusion(movie.Title, releaseExclusion.Value));
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
                monitored => movieClient.GetMissingMoviesAsync(arrInstance, monitored, cancellationToken),
                instanceConfig.MonitoredOnly);
            missingIds = missing.Select(m => m.Id).ToHashSet();

            if (instanceConfig.UseCutoff)
            {
                List<SearchableMovie> cutoffUnmet = await ReadWantedAsync(
                    monitored => movieClient.GetCutoffUnmetMoviesAsync(arrInstance, monitored, cancellationToken),
                    instanceConfig.MonitoredOnly);
                cutoffUnmetIds = cutoffUnmet.Select(m => m.Id).ToHashSet();
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            string minimumVersion = instanceType is InstanceType.Radarr ? "Radarr v5.6" : "Whisparr v3.1";
            _logger.LogWarning(ex, "Skipping proactive search for {InstanceName}, it needs {MinimumVersion} or newer", arrInstance.Name, minimumVersion);

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
            SeekerUnitRest? rest = null;
            if (queuedMovieIds.Contains(movie.Id))
            {
                blockedBy = SeekerExclusionReason.InQueue;
            }
            else if (recentlyGrabbedMovieIds.Contains(movie.Id))
            {
                blockedBy = SeekerExclusionReason.RecentlyGrabbed;
            }
            else if (rests.TryGetValue((movie.Id, 0, 0), out rest))
            {
                blockedBy = SeekerExclusionReason.Resting;
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
                Rest = rest,
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
            // Keeps this run's rests, so a rest that ends with the rollover ends one run late
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
        Dictionary<(long ItemId, int SeasonNumber, long EpisodeId), SeekerUnitRest> rests,
        List<SeekerHistory> currentCycleHistory,
        Dictionary<long, DateTimeOffset> lastSearchedByItem,
        CancellationToken cancellationToken)
    {
        List<Tag> tags = await SeriesClient(arrInstance).GetAllTagsAsync(arrInstance);
        Dictionary<long, string> tagsById = tags.ToDictionary(t => t.Id, t => t.Label);
        HashSet<string> skipTagSet = new(instanceConfig.SkipTags, StringComparer.InvariantCultureIgnoreCase);
        HashSet<string> includeTagSet = new(instanceConfig.IncludeTags, StringComparer.InvariantCultureIgnoreCase);

        HashSet<int> upgradesDisabledProfileIds = [];
        if (instanceConfig.UseCustomFormatScore)
        {
            List<ArrQualityProfile> profiles = await SeriesClient(arrInstance).GetQualityProfilesAsync(arrInstance);
            upgradesDisabledProfileIds = profiles.Where(p => !p.UpgradeAllowed).Select(p => p.Id).ToHashSet();
        }

        DateTimeOffset graceCutoff = _timeProvider.GetUtcNow().AddHours(-config.PostReleaseGraceHours);

        List<long> allLibraryIds = new();
        List<SeekerExclusion> exclusions = [];
        int releasedCount = 0;
        int monitoredCount = 0;
        Dictionary<long, SearchableSeries> seriesById = new();
        await foreach (SearchableSeries series in SeriesClient(arrInstance).StreamAllSeriesAsync(arrInstance, cancellationToken))
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

            ExcludeEpisode(e.SeriesId, e.Id, SeekerApps.EpisodeName(arrInstance, series.Title, e.SeasonNumber, e.EpisodeNumber, e.AirDateUtc),
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
            monitored => SeriesClient(arrInstance).GetMissingEpisodesAsync(arrInstance, monitored, cancellationToken),
            instanceConfig.MonitoredOnly);
        wanted.AddRange(missing
            .Where(IsReleasedEpisodeOfCandidate)
            .Select(e => ToWanted(e, SeekerSearchReason.Missing)));

        if (instanceConfig.UseCutoff)
        {
            List<SearchableEpisode> cutoffUnmet = await ReadWantedAsync(
                monitored => SeriesClient(arrInstance).GetCutoffUnmetEpisodesAsync(arrInstance, monitored, cancellationToken),
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
                    && e.ItemType == arrInstance.ArrConfig.Type
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

        SeekerSearchUnit WithBlockedBy(SeekerSearchUnit unit)
        {
            bool queued = unit.EpisodeId == 0
                ? queuedSeasons.Contains((unit.ItemId, unit.SeasonNumber))
                : queuedEpisodeIds.Contains(unit.EpisodeId);
            if (queued)
            {
                return unit with { BlockedBy = SeekerExclusionReason.InQueue };
            }

            bool grabbed = unit.EpisodeId == 0
                ? grabbedSeasons.Contains((unit.ItemId, unit.SeasonNumber))
                : grabbedEpisodeIds.Contains(unit.EpisodeId);
            if (grabbed)
            {
                return unit with { BlockedBy = SeekerExclusionReason.RecentlyGrabbed };
            }

            return rests.TryGetValue((unit.ItemId, unit.SeasonNumber, unit.EpisodeId), out SeekerUnitRest? rest)
                ? unit with { BlockedBy = SeekerExclusionReason.Resting, Rest = rest }
                : unit;
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
                Units = BuildSonarrUnits(arrInstance, wanted, seriesById, finishedAnimeSeasons, searchedSeasons, searchedEpisodeIds, instanceConfig.SonarrSearchMode)
                    .Select(WithBlockedBy)
                    .ToList(),
                LastSearchedByItem = searched,
                // Keeps this run's rests, so a rest that ends with the rollover ends one run late
                WithoutCycleHistory = () => Build([], new Dictionary<long, DateTimeOffset>()),
            };
        }

        return Build(currentCycleHistory, lastSearchedByItem);
    }

    private async Task<SeekerEvaluation> EvaluateLidarrAsync(
        SeekerConfig config,
        SeekerInstanceConfig instanceConfig,
        ArrInstance arrInstance,
        List<QueueRecord> activeQueueRecords,
        HashSet<long> recentlyGrabbedAlbumIds,
        Dictionary<(long ItemId, int SeasonNumber, long EpisodeId), SeekerUnitRest> rests,
        List<SeekerHistory> currentCycleHistory,
        Dictionary<long, DateTimeOffset> lastSearchedByItem,
        CancellationToken cancellationToken)
    {
        List<Tag> tags = await _lidarrClient.GetAllTagsAsync(arrInstance);
        Dictionary<long, string> tagsById = tags.ToDictionary(t => t.Id, t => t.Label);
        HashSet<string> skipTagSet = new(instanceConfig.SkipTags, StringComparer.InvariantCultureIgnoreCase);
        HashSet<string> includeTagSet = new(instanceConfig.IncludeTags, StringComparer.InvariantCultureIgnoreCase);

        HashSet<int> upgradesDisabledProfileIds = [];
        if (instanceConfig.UseCustomFormatScore)
        {
            List<ArrQualityProfile> profiles = await _lidarrClient.GetQualityProfilesAsync(arrInstance);
            upgradesDisabledProfileIds = profiles.Where(p => !p.UpgradeAllowed).Select(p => p.Id).ToHashSet();
        }

        DateTimeOffset graceCutoff = _timeProvider.GetUtcNow().AddHours(-config.PostReleaseGraceHours);

        List<long> allLibraryIds = new();
        List<SeekerExclusion> exclusions = [];
        int releasedCount = 0;
        int monitoredCount = 0;
        Dictionary<long, SearchableArtist> artistsById = new();
        await foreach (SearchableArtist artist in _lidarrClient.StreamAllArtistsAsync(arrInstance, cancellationToken))
        {
            allLibraryIds.Add(artist.Id);
            releasedCount++;

            if (instanceConfig.MonitoredOnly && !artist.Monitored)
            {
                exclusions.Add(new SeekerExclusion(artist.ArtistName, SeekerExclusionReason.NotMonitored));
                continue;
            }

            monitoredCount++;

            SeekerExclusionReason? tagExclusion = GetTagExclusion(artist.Tags, tagsById, skipTagSet, includeTagSet);
            if (tagExclusion is not null)
            {
                exclusions.Add(new SeekerExclusion(artist.ArtistName, tagExclusion.Value));
                continue;
            }

            artistsById[artist.Id] = artist;
        }

        List<SonarrWantedEpisode> wanted = [];
        DateTimeOffset now = _timeProvider.GetUtcNow();
        HashSet<long> excludedAlbumIds = [];
        HashSet<long> artistsWithExcludedAlbums = [];

        void ExcludeAlbum(long artistId, long albumId, string name, SeekerExclusionReason reason)
        {
            artistsWithExcludedAlbums.Add(artistId);
            if (excludedAlbumIds.Add(albumId))
            {
                exclusions.Add(new SeekerExclusion(name, reason));
            }
        }

        bool IsReleasedAlbumOfCandidate(SearchableAlbum a)
        {
            if (!artistsById.TryGetValue(a.ArtistId, out SearchableArtist? artist))
            {
                return false;
            }

            if (a.ReleaseDate is null || a.ReleaseDate.Value <= graceCutoff)
            {
                return true;
            }

            ExcludeAlbum(a.ArtistId, a.Id, $"{artist.ArtistName} - {a.Title}",
                a.ReleaseDate.Value > now
                    ? SeekerExclusionReason.NotReleased
                    : SeekerExclusionReason.InGracePeriod);
            return false;
        }

        SonarrWantedEpisode ToWanted(SearchableAlbum a, SeekerSearchReason reason) => new()
        {
            SeriesId = a.ArtistId,
            SeasonNumber = 0,
            EpisodeId = a.Id,
            Title = $"{artistsById[a.ArtistId].ArtistName} - {a.Title}",
            AirDateUtc = a.ReleaseDate,
            LastSearchTime = a.LastSearchTime,
            Monitored = a.Monitored,
            Reason = reason,
        };

        List<SearchableAlbum> missing = await ReadWantedAsync(
            monitored => _lidarrClient.GetMissingAlbumsAsync(arrInstance, monitored, cancellationToken),
            instanceConfig.MonitoredOnly);
        wanted.AddRange(missing
            .Where(IsReleasedAlbumOfCandidate)
            .Select(a => ToWanted(a, SeekerSearchReason.Missing)));

        if (instanceConfig.UseCutoff)
        {
            List<SearchableAlbum> cutoffUnmet = await ReadWantedAsync(
                monitored => _lidarrClient.GetCutoffUnmetAlbumsAsync(arrInstance, monitored, cancellationToken),
                instanceConfig.MonitoredOnly);
            wanted.AddRange(cutoffUnmet
                .Where(IsReleasedAlbumOfCandidate)
                .Select(a => ToWanted(a, SeekerSearchReason.QualityCutoffNotMet)));
        }

        if (instanceConfig.UseCustomFormatScore)
        {
            List<CustomFormatScoreEntry> belowCutoff = await _eventsContext.CustomFormatScoreEntries
                .AsNoTracking()
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == InstanceType.Lidarr
                    && (e.AirDateUtc == null || e.AirDateUtc <= graceCutoff)
                    && e.CurrentScore < e.CutoffScore
                    && (!instanceConfig.MonitoredOnly || e.IsMonitored))
                .ToListAsync(cancellationToken);

            HashSet<long> wantedAlbumIds = wanted.Select(w => w.EpisodeId).ToHashSet();

            foreach (CustomFormatScoreEntry entry in belowCutoff)
            {
                if (!artistsById.TryGetValue(entry.ExternalItemId, out SearchableArtist? artist))
                {
                    continue;
                }

                if (upgradesDisabledProfileIds.Contains(artist.QualityProfileId))
                {
                    if (!wantedAlbumIds.Contains(entry.EpisodeId))
                    {
                        _logger.LogDebug("skip | upgrades disabled in profile | {Title} | {InstanceName}", entry.Title, arrInstance.Name);
                        ExcludeAlbum(entry.ExternalItemId, entry.EpisodeId, entry.Title, SeekerExclusionReason.UpgradesDisabled);
                    }

                    continue;
                }

                wanted.Add(new SonarrWantedEpisode
                {
                    SeriesId = entry.ExternalItemId,
                    SeasonNumber = 0,
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

        HashSet<long> wantedArtistIds = wanted.Select(w => w.SeriesId).ToHashSet();
        exclusions.AddRange(artistsById.Values
            .Where(a => !wantedArtistIds.Contains(a.Id) && !artistsWithExcludedAlbums.Contains(a.Id))
            .Select(a => new SeekerExclusion(a.ArtistName, SeekerExclusionReason.NothingToSearch)));

        HashSet<long> queuedAlbumIds = activeQueueRecords
            .Where(r => r.AlbumId > 0)
            .Select(r => r.AlbumId)
            .ToHashSet();

        SeekerEvaluation Build(List<SeekerHistory> history, Dictionary<long, DateTimeOffset> searched)
        {
            HashSet<long> searchedAlbumIds = history
                .Where(h => h.EpisodeId > 0)
                .Select(h => h.EpisodeId)
                .ToHashSet();

            List<SeekerSearchUnit> units = [];
            foreach (IGrouping<long, SonarrWantedEpisode> album in wanted.GroupBy(w => w.EpisodeId))
            {
                SonarrWantedEpisode first = album.First();
                SeekerExclusionReason? blockedBy = null;
                SeekerUnitRest? rest = null;
                if (queuedAlbumIds.Contains(album.Key))
                {
                    blockedBy = SeekerExclusionReason.InQueue;
                }
                else if (recentlyGrabbedAlbumIds.Contains(album.Key))
                {
                    blockedBy = SeekerExclusionReason.RecentlyGrabbed;
                }
                else if (rests.TryGetValue((first.SeriesId, 0, album.Key), out rest))
                {
                    blockedBy = SeekerExclusionReason.Resting;
                }

                units.Add(new SeekerSearchUnit
                {
                    ItemId = first.SeriesId,
                    SeasonNumber = 0,
                    EpisodeId = album.Key,
                    Name = first.Title!,
                    Reasons = album.Select(w => w.Reason).ToList(),
                    AirDateUtc = first.AirDateUtc,
                    Added = artistsById[first.SeriesId].Added,
                    LastSearchTime = album.Max(w => w.LastSearchTime),
                    CustomFormatGap = album.Max(w => w.CustomFormatGap),
                    Searched = searchedAlbumIds.Contains(album.Key),
                    BlockedBy = blockedBy,
                    Rest = rest,
                });
            }

            return new SeekerEvaluation
            {
                AllLibraryIds = allLibraryIds,
                ReleasedItems = releasedCount,
                MonitoredItems = monitoredCount,
                TagFilteredItems = artistsById.Count,
                ItemExclusions = exclusions,
                Units = units,
                LastSearchedByItem = searched,
                // Keeps this run's rests, so a rest that ends with the rollover ends one run late
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
        ArrInstance arrInstance,
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
                    Name = SeekerApps.SeasonName(arrInstance, series.Title, season.Key.SeasonNumber),
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
                    Name = SeekerApps.NamesScenesByDate(arrInstance) || episodeNumber is not null
                        ? SeekerApps.EpisodeName(arrInstance, series.Title, season.Key.SeasonNumber, episodeNumber ?? 0, first.AirDateUtc)
                        : first.Title ?? SeekerApps.SeasonName(arrInstance, series.Title, season.Key.SeasonNumber),
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
    /// Under <see cref="SelectionStrategy.CustomFormatGapWeighted"/> it picks the missing unit, else the largest gap unit.
    /// </summary>
    private static SeekerSearchUnit PickUnit(List<SeekerSearchUnit> itemUnits, SelectionStrategy strategy)
    {
        if (strategy is SelectionStrategy.CustomFormatGapWeighted)
        {
            return itemUnits
                .OrderByDescending(u => u.Reasons.Contains(SeekerSearchReason.Missing))
                .ThenByDescending(u => u.CustomFormatGap)
                .ThenBy(u => u.AirDateUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(u => u.EpisodeId)
                .First();
        }

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
                episodes = await SeriesClient(arrInstance).GetEpisodesAsync(arrInstance, seriesId, cancellationToken);
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
        DateTimeOffset? releaseDate = movie.DigitalRelease ?? movie.PhysicalRelease ?? movie.InCinemas ?? movie.ReleaseDate;
        return releaseDate is null || releaseDate.Value <= graceCutoff;
    }

    /// <summary>
    /// Returns why the release gate keeps the movie out, or null when it lets it through.
    /// </summary>
    private static SeekerExclusionReason? GetReleaseExclusion(
        SearchableMovie movie,
        RadarrReleaseGate gate,
        DateTimeOffset graceCutoff)
    {
        if (gate is RadarrReleaseGate.ArrAvailability)
        {
            return movie.IsAvailable ? null : SeekerExclusionReason.NotAvailable;
        }

        if (movie.Status is not "released")
        {
            return SeekerExclusionReason.NotReleased;
        }

        return IsMoviePastGracePeriod(movie, graceCutoff) ? null : SeekerExclusionReason.InGracePeriod;
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
