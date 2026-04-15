using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

/// <summary>
/// Periodically syncs custom format scores from Radarr/Sonarr instances.
/// Tracks score changes over time for dashboard display and Seeker filtering.
/// </summary>
public sealed class CustomFormatScoreSyncer : IHandler
{
    private const int ChunkSize = 200;
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(120);

    private readonly ILogger<CustomFormatScoreSyncer> _logger;
    private readonly DataContext _dataContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly TimeProvider _timeProvider;
    private readonly IHubContext<AppHub> _hubContext;

    public CustomFormatScoreSyncer(
        ILogger<CustomFormatScoreSyncer> logger,
        DataContext dataContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        TimeProvider timeProvider,
        IHubContext<AppHub> hubContext)
    {
        _logger = logger;
        _dataContext = dataContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _timeProvider = timeProvider;
        _hubContext = hubContext;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        List<SeekerInstanceConfig> instanceConfigs = await _dataContext.SeekerInstanceConfigs
            .Include(s => s.ArrInstance)
                .ThenInclude(a => a.ArrConfig)
            .Where(s => s.Enabled && s.ArrInstance.Enabled && s.UseCustomFormatScore)
            .ToListAsync(cancellationToken: cancellationToken);

        instanceConfigs = instanceConfigs
            .Where(s => s.ArrInstance.ArrConfig.Type is InstanceType.Sonarr or InstanceType.Radarr)
            .ToList();

        _logger.LogDebug("CF score sync found {Count} enabled instance(s): {Instances}",
            instanceConfigs.Count,
            string.Join(", ", instanceConfigs.Select(c => $"{c.ArrInstance.Name} ({c.ArrInstance.ArrConfig.Type})")));

        if (instanceConfigs.Count == 0)
        {
            _logger.LogDebug("No enabled instances for CF score sync");
            return;
        }

        foreach (SeekerInstanceConfig instanceConfig in instanceConfigs)
        {
            try
            {
                await SyncInstanceAsync(instanceConfig.ArrInstance, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync CF scores for {InstanceName}",
                    instanceConfig.ArrInstance.Name);
            }
        }

        await CleanupOldHistoryAsync();

        await _hubContext.Clients.All.SendAsync("CfScoresUpdated", cancellationToken: cancellationToken);
    }

    private async Task SyncInstanceAsync(ArrInstance arrInstance, CancellationToken cancellationToken)
    {
        InstanceType instanceType = arrInstance.ArrConfig.Type;

        _logger.LogDebug("Syncing CF scores for {InstanceType} instance: {InstanceName}",
            instanceType, arrInstance.Name);

        if (instanceType == InstanceType.Radarr)
        {
            await SyncRadarrAsync(arrInstance);
        }
        else
        {
            await SyncSonarrAsync(arrInstance, cancellationToken);
        }
    }

    private async Task SyncRadarrAsync(ArrInstance arrInstance)
    {
        List<ArrQualityProfile> profiles = await _radarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        _logger.LogTrace("[Radarr] {InstanceName}: fetched {ProfileCount} quality profile(s)",
            arrInstance.Name, profiles.Count);

        List<SearchableMovie> allMovies = await _radarrClient.GetAllMoviesAsync(arrInstance);

        List<(SearchableMovie Movie, long FileId)> moviesWithFiles = allMovies
            .Where(m => m.HasFile && m.MovieFile is not null)
            .Select(m => (Movie: m, FileId: m.MovieFile!.Id))
            .Where(x => x.FileId > 0)
            .ToList();

        _logger.LogTrace("[Radarr] {InstanceName}: found {TotalMovies} total movies, {WithFiles} with files",
            arrInstance.Name, allMovies.Count, moviesWithFiles.Count);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        // Touch entries for movies that still exist but have no file (e.g., RSS upgrade in progress)
        HashSet<long> movieIdsWithFiles = moviesWithFiles.Select(x => x.Movie.Id).ToHashSet();
        List<long> movieIdsWithoutFiles = allMovies
            .Where(m => !movieIdsWithFiles.Contains(m.Id))
            .Select(m => m.Id)
            .ToList();

        foreach (long[] touchChunk in movieIdsWithoutFiles.Chunk(ChunkSize))
        {
            List<long> chunkList = touchChunk.ToList();
            await _dataContext.CustomFormatScoreEntries
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == InstanceType.Radarr
                    && chunkList.Contains(e.ExternalItemId))
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSyncedAt, now));
        }

        int totalSynced = 0;
        int totalSkipped = 0;

        foreach ((SearchableMovie Movie, long FileId)[] chunk in moviesWithFiles.Chunk(ChunkSize))
        {
            List<long> fileIds = chunk.Select(x => x.FileId).ToList();
            Dictionary<long, int> scores = await _radarrClient.GetMovieFileScoresAsync(arrInstance, fileIds);

            _logger.LogTrace("[Radarr] {InstanceName}: chunk of {FileCount} file IDs returned {ScoreCount} score(s)",
                arrInstance.Name, fileIds.Count, scores.Count);

            List<long> movieIds = chunk.Select(x => x.Movie.Id).ToList();
            Dictionary<long, CustomFormatScoreEntry> existingEntries = await _dataContext.CustomFormatScoreEntries
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == InstanceType.Radarr
                    && movieIds.Contains(e.ExternalItemId))
                .ToDictionaryAsync(e => e.ExternalItemId);

            foreach ((SearchableMovie movie, long fileId) in chunk)
            {
                if (!scores.TryGetValue(fileId, out int cfScore))
                {
                    totalSkipped++;
                    // Touch existing entry to prevent stale cleanup — movie still exists
                    if (existingEntries.TryGetValue(movie.Id, out CustomFormatScoreEntry? skippedEntry))
                    {
                        skippedEntry.LastSyncedAt = now;
                    }
                    _logger.LogTrace("[Radarr] {InstanceName}: skipping movie '{Title}' (fileId={FileId}) — no score returned",
                        arrInstance.Name, movie.Title, fileId);
                    continue;
                }

                profileMap.TryGetValue(movie.QualityProfileId, out ArrQualityProfile? profile);
                int cutoffScore = profile?.CutoffFormatScore ?? 0;
                string profileName = profile?.Name ?? "Unknown";

                existingEntries.TryGetValue(movie.Id, out CustomFormatScoreEntry? existing);
                UpsertCustomFormatScore(existing, arrInstance.Id, movie.Id, 0, InstanceType.Radarr, movie.Title, fileId, cfScore, cutoffScore, profileName, movie.Monitored, now);

                totalSynced++;
            }

            await _dataContext.SaveChangesAsync();
        }

        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Radarr, now);

        _logger.LogInformation("[Radarr] Synced CF scores for {Count} movies on {InstanceName} ({Skipped} skipped)",
            totalSynced, arrInstance.Name, totalSkipped);
    }

    private async Task SyncSonarrAsync(ArrInstance arrInstance, CancellationToken cancellationToken)
    {
        List<ArrQualityProfile> profiles = await _sonarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        _logger.LogTrace("[Sonarr] {InstanceName}: fetched {ProfileCount} quality profile(s)",
            arrInstance.Name, profiles.Count);

        List<SearchableSeries> allSeries = await _sonarrClient.GetAllSeriesAsync(arrInstance);

        _logger.LogTrace("[Sonarr] {InstanceName}: found {SeriesCount} total series",
            arrInstance.Name, allSeries.Count);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        int totalSynced = 0;
        int totalSkipped = 0;

        foreach (SearchableSeries[] chunk in allSeries.Chunk(ChunkSize))
        {
            // Collect all episodes with files for this chunk of series
            List<(SearchableSeries Series, SearchableEpisode Episode, long FileId, int CfScore, bool IsMonitored)> itemsInChunk = [];
            List<(long SeriesId, long EpisodeId)> episodesToTouch = [];

            foreach (SearchableSeries series in chunk)
            {
                try
                {
                    List<SearchableEpisode> episodes = await _sonarrClient.GetEpisodesAsync(arrInstance, series.Id);
                    List<ArrEpisodeFile> episodeFiles = await _sonarrClient.GetEpisodeFilesAsync(arrInstance, series.Id);

                    // Build a map of fileId -> episode file
                    Dictionary<long, ArrEpisodeFile> fileMap = episodeFiles.ToDictionary(f => f.Id);

                    // Match episodes to their files via EpisodeFileId
                    int matched = 0;
                    foreach (SearchableEpisode episode in episodes)
                    {
                        if (episode.EpisodeFileId > 0 && fileMap.TryGetValue(episode.EpisodeFileId, out ArrEpisodeFile? file))
                        {
                            itemsInChunk.Add((series, episode, file.Id, file.CustomFormatScore, series.Monitored && episode.Monitored));
                            matched++;
                        }
                        else
                        {
                            // Episode exists but has no file — touch its entry to prevent stale cleanup
                            episodesToTouch.Add((series.Id, episode.Id));
                            if (episode.EpisodeFileId > 0)
                            {
                                totalSkipped++;
                            }
                        }
                    }

                    _logger.LogTrace("[Sonarr] {InstanceName}: series '{SeriesTitle}' (id={SeriesId}) has {TotalEpisodes} episodes, {FileCount} files, {Matched} matched",
                        arrInstance.Name, series.Title, series.Id, episodes.Count, episodeFiles.Count, matched);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Sonarr] Failed to fetch episodes for series '{SeriesTitle}' (id={SeriesId}) on {InstanceName}",
                        series.Title, series.Id, arrInstance.Name);
                }

                // Rate limit to avoid overloading the Sonarr API
                await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);
            }

            if (itemsInChunk.Count > 0)
            {
                List<long> seriesIds = itemsInChunk.Select(x => x.Series.Id).Distinct().ToList();
                Dictionary<(long, long), CustomFormatScoreEntry> existingEntries = await _dataContext.CustomFormatScoreEntries
                    .Where(e => e.ArrInstanceId == arrInstance.Id
                        && e.ItemType == InstanceType.Sonarr
                        && seriesIds.Contains(e.ExternalItemId))
                    .ToDictionaryAsync(e => (e.ExternalItemId, e.EpisodeId));

                foreach ((SearchableSeries series, SearchableEpisode episode, long fileId, int cfScore, bool isMonitored) in itemsInChunk)
                {
                    profileMap.TryGetValue(series.QualityProfileId, out ArrQualityProfile? profile);
                    int cutoffScore = profile?.CutoffFormatScore ?? 0;
                    string profileName = profile?.Name ?? "Unknown";

                    string title = $"{series.Title} S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";

                    existingEntries.TryGetValue((series.Id, episode.Id), out CustomFormatScoreEntry? existing);
                    UpsertCustomFormatScore(existing, arrInstance.Id, series.Id, episode.Id, InstanceType.Sonarr, title, fileId, cfScore, cutoffScore, profileName, isMonitored, now);

                    totalSynced++;
                }

                await _dataContext.SaveChangesAsync();
            }
            else
            {
                _logger.LogTrace("[Sonarr] {InstanceName}: chunk of {ChunkSize} series yielded 0 episodes with files",
                    arrInstance.Name, chunk.Length);
            }

            // Touch entries for episodes that exist but have no file (e.g., RSS upgrade in progress)
            foreach (var group in episodesToTouch.GroupBy(x => x.SeriesId))
            {
                List<long> episodeIds = group.Select(x => x.EpisodeId).ToList();
                foreach (long[] epChunk in episodeIds.Chunk(ChunkSize))
                {
                    List<long> epChunkList = epChunk.ToList();
                    await _dataContext.CustomFormatScoreEntries
                        .Where(e => e.ArrInstanceId == arrInstance.Id
                            && e.ItemType == InstanceType.Sonarr
                            && e.ExternalItemId == group.Key
                            && epChunkList.Contains(e.EpisodeId))
                        .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSyncedAt, now));
                }
            }
        }

        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Sonarr, now);

        _logger.LogInformation("[Sonarr] Synced CF scores for {Count} episodes on {InstanceName} ({Skipped} skipped)",
            totalSynced, arrInstance.Name, totalSkipped);
    }

    /// <summary>
    /// Creates or updates a CF score entry and records history when the score changes.
    /// </summary>
    private void UpsertCustomFormatScore(
        CustomFormatScoreEntry? existing,
        Guid arrInstanceId,
        long externalItemId,
        long episodeId,
        InstanceType itemType,
        string title,
        long fileId,
        int cfScore,
        int cutoffScore,
        string profileName,
        bool isMonitored,
        DateTime now)
    {
        if (existing is not null)
        {
            if (existing.CurrentScore != cfScore)
            {
                _dataContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
                {
                    ArrInstanceId = arrInstanceId,
                    ExternalItemId = externalItemId,
                    EpisodeId = episodeId,
                    ItemType = itemType,
                    Title = title,
                    Score = cfScore,
                    CutoffScore = cutoffScore,
                    RecordedAt = now,
                });
            }

            existing.CurrentScore = cfScore;
            existing.CutoffScore = cutoffScore;
            existing.FileId = fileId;
            existing.QualityProfileName = profileName;
            existing.Title = title;
            existing.IsMonitored = isMonitored;
            existing.LastSyncedAt = now;
        }
        else
        {
            _dataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
            {
                ArrInstanceId = arrInstanceId,
                ExternalItemId = externalItemId,
                EpisodeId = episodeId,
                ItemType = itemType,
                Title = title,
                FileId = fileId,
                CurrentScore = cfScore,
                CutoffScore = cutoffScore,
                QualityProfileName = profileName,
                IsMonitored = isMonitored,
                LastSyncedAt = now,
            });

            // Record initial score in history
            _dataContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
            {
                ArrInstanceId = arrInstanceId,
                ExternalItemId = externalItemId,
                EpisodeId = episodeId,
                ItemType = itemType,
                Title = title,
                Score = cfScore,
                CutoffScore = cutoffScore,
                RecordedAt = now,
            });
        }
    }

    /// <summary>
    /// Removes CF score entries and history for items not seen during the current sync.
    /// Items that still exist in the *arr app (even without files) have their LastSyncedAt
    /// updated during sync, so any entry with LastSyncedAt &lt; syncStartTime is truly removed.
    /// </summary>
    private async Task CleanupStaleEntriesAsync(
        Guid arrInstanceId, InstanceType instanceType, DateTime syncStartTime)
    {
        List<long> staleItemIds = await _dataContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstanceId
                && e.ItemType == instanceType
                && e.LastSyncedAt < syncStartTime)
            .Select(e => e.ExternalItemId)
            .Distinct()
            .ToListAsync();

        if (staleItemIds.Count == 0)
        {
            return;
        }

        await _dataContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstanceId
                && e.ItemType == instanceType
                && e.LastSyncedAt < syncStartTime)
            .ExecuteDeleteAsync();

        foreach (long[] chunk in staleItemIds.Chunk(ChunkSize))
        {
            List<long> chunkList = chunk.ToList();
            await _dataContext.CustomFormatScoreHistory
                .Where(h => h.ArrInstanceId == arrInstanceId
                    && h.ItemType == instanceType
                    && chunkList.Contains(h.ExternalItemId))
                .ExecuteDeleteAsync();
        }

        _logger.LogTrace("Cleaned up {Count} stale CF score item(s) for instance {InstanceId}",
            staleItemIds.Count, arrInstanceId);
    }

    /// <summary>
    /// Removes CF score history entries older than the retention period
    /// </summary>
    private async Task CleanupOldHistoryAsync()
    {
        DateTime threshold = _timeProvider.GetUtcNow().UtcDateTime - HistoryRetention;
        int deleted = await _dataContext.CustomFormatScoreHistory
            .Where(h => h.RecordedAt < threshold)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogDebug("Cleaned up {Count} CF score history entries older than {Days} days",
                deleted, HistoryRetention.Days);
        }
    }
}
