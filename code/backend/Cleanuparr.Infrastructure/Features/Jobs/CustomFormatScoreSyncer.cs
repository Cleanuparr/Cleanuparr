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
    private const int CheckpointIntervalChunks = 10;
    private const int FetchConcurrency = 5;
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(120);

    private sealed record SeriesFetch(SearchableSeries Series, List<SearchableEpisode> Episodes, List<ArrEpisodeFile> EpisodeFiles, bool Failed);

    private readonly ILogger<CustomFormatScoreSyncer> _logger;
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly TimeProvider _timeProvider;
    private readonly IHubContext<AppHub> _hubContext;

    public CustomFormatScoreSyncer(
        ILogger<CustomFormatScoreSyncer> logger,
        DataContext dataContext,
        EventsContext eventsContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        TimeProvider timeProvider,
        IHubContext<AppHub> hubContext)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _timeProvider = timeProvider;
        _hubContext = hubContext;
    }

    private async Task ConfigureSqliteCacheSizeAsync(CancellationToken cancellationToken)
    {
        if (_eventsContext.Database.IsSqlite())
        {
            await _eventsContext.Database.ExecuteSqlRawAsync("PRAGMA cache_size=-20000;", cancellationToken);
        }
    }

    private async Task CheckpointWalIfDueAsync(int flushedChunks, CancellationToken cancellationToken)
    {
        if (_eventsContext.Database.IsSqlite() && flushedChunks % CheckpointIntervalChunks == 0)
        {
            await _eventsContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);
        }
    }

    private async Task CheckpointWalAsync(CancellationToken cancellationToken)
    {
        if (_eventsContext.Database.IsSqlite())
        {
            await _eventsContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);
        }
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
            await SyncRadarrAsync(arrInstance, cancellationToken);
        }
        else
        {
            await SyncSonarrAsync(arrInstance, cancellationToken);
        }
    }

    private async Task SyncRadarrAsync(ArrInstance arrInstance, CancellationToken cancellationToken)
    {
        await ConfigureSqliteCacheSizeAsync(cancellationToken);

        List<ArrQualityProfile> profiles = await _radarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        _logger.LogTrace("[Radarr] {InstanceName}: fetched {ProfileCount} quality profile(s)",
            arrInstance.Name, profiles.Count);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        List<(SearchableMovie Movie, long FileId)> withFilesBuffer = new(ChunkSize);
        List<long> withoutFilesBuffer = new(ChunkSize);

        int totalMovies = 0;
        int totalWithFiles = 0;
        int totalSynced = 0;
        int totalSkipped = 0;
        int flushedChunks = 0;

        await foreach (SearchableMovie movie in _radarrClient.StreamAllMoviesAsync(arrInstance, cancellationToken))
        {
            totalMovies++;

            if (movie.HasFile && movie.MovieFile is not null && movie.MovieFile.Id > 0)
            {
                withFilesBuffer.Add((movie, movie.MovieFile.Id));
                totalWithFiles++;

                if (withFilesBuffer.Count >= ChunkSize)
                {
                    (int synced, int skipped) = await FlushRadarrWithFilesChunkAsync(arrInstance, profileMap, withFilesBuffer, now);
                    totalSynced += synced;
                    totalSkipped += skipped;
                    withFilesBuffer.Clear();
                    flushedChunks++;
                    await CheckpointWalIfDueAsync(flushedChunks, cancellationToken);
                }
            }
            else
            {
                withoutFilesBuffer.Add(movie.Id);

                if (withoutFilesBuffer.Count >= ChunkSize)
                {
                    await FlushRadarrTouchChunkAsync(arrInstance, withoutFilesBuffer, now);
                    withoutFilesBuffer.Clear();
                    flushedChunks++;
                    await CheckpointWalIfDueAsync(flushedChunks, cancellationToken);
                }
            }
        }

        if (withFilesBuffer.Count > 0)
        {
            (int synced, int skipped) = await FlushRadarrWithFilesChunkAsync(arrInstance, profileMap, withFilesBuffer, now);
            totalSynced += synced;
            totalSkipped += skipped;
            withFilesBuffer.Clear();
            flushedChunks++;
        }

        if (withoutFilesBuffer.Count > 0)
        {
            await FlushRadarrTouchChunkAsync(arrInstance, withoutFilesBuffer, now);
            withoutFilesBuffer.Clear();
            flushedChunks++;
        }

        _logger.LogTrace("[Radarr] {InstanceName}: found {TotalMovies} total movies, {WithFiles} with files",
            arrInstance.Name, totalMovies, totalWithFiles);

        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Radarr, now);

        await CheckpointWalAsync(cancellationToken);

        _logger.LogInformation("[Radarr] Synced CF scores for {Count} movies on {InstanceName} ({Skipped} skipped)",
            totalSynced, arrInstance.Name, totalSkipped);
    }

    private async Task FlushRadarrTouchChunkAsync(ArrInstance arrInstance, List<long> movieIds, DateTimeOffset now)
    {
        List<long> chunkList = movieIds.ToList();
        await _eventsContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstance.Id
                && e.ItemType == InstanceType.Radarr
                && chunkList.Contains(e.ExternalItemId))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSyncedAt, now));
    }

    private async Task<(int Synced, int Skipped)> FlushRadarrWithFilesChunkAsync(
        ArrInstance arrInstance,
        Dictionary<int, ArrQualityProfile> profileMap,
        List<(SearchableMovie Movie, long FileId)> chunk,
        DateTimeOffset now)
    {
        int synced = 0;
        int skipped = 0;

        List<long> fileIds = chunk.Select(x => x.FileId).ToList();
        Dictionary<long, int> scores = await _radarrClient.GetMovieFileScoresAsync(arrInstance, fileIds);

        _logger.LogTrace("[Radarr] {InstanceName}: chunk of {FileCount} file IDs returned {ScoreCount} score(s)",
            arrInstance.Name, fileIds.Count, scores.Count);

        List<long> movieIds = chunk.Select(x => x.Movie.Id).ToList();
        Dictionary<long, CustomFormatScoreEntry> existingEntries = await _eventsContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstance.Id
                && e.ItemType == InstanceType.Radarr
                && movieIds.Contains(e.ExternalItemId))
            .ToDictionaryAsync(e => e.ExternalItemId);

        bool autoDetect = _eventsContext.ChangeTracker.AutoDetectChangesEnabled;
        _eventsContext.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach ((SearchableMovie movie, long fileId) in chunk)
            {
                if (!scores.TryGetValue(fileId, out int cfScore))
                {
                    skipped++;
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

                synced++;
            }

            _eventsContext.ChangeTracker.DetectChanges();
            await _eventsContext.SaveChangesAsync();
            _eventsContext.ChangeTracker.Clear();
        }
        finally
        {
            _eventsContext.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
        }

        return (synced, skipped);
    }

    private async Task SyncSonarrAsync(ArrInstance arrInstance, CancellationToken cancellationToken)
    {
        await ConfigureSqliteCacheSizeAsync(cancellationToken);

        List<ArrQualityProfile> profiles = await _sonarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        _logger.LogTrace("[Sonarr] {InstanceName}: fetched {ProfileCount} quality profile(s)",
            arrInstance.Name, profiles.Count);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        List<SearchableSeries> buffer = new(ChunkSize);

        int totalSeries = 0;
        int totalSynced = 0;
        int totalSkipped = 0;
        int flushedChunks = 0;

        await foreach (SearchableSeries series in _sonarrClient.StreamAllSeriesAsync(arrInstance, cancellationToken))
        {
            totalSeries++;
            buffer.Add(series);

            if (buffer.Count >= ChunkSize)
            {
                (int synced, int skipped) = await FlushSonarrSeriesChunkAsync(arrInstance, profileMap, buffer, now, cancellationToken);
                totalSynced += synced;
                totalSkipped += skipped;
                buffer.Clear();
                flushedChunks++;
                await CheckpointWalIfDueAsync(flushedChunks, cancellationToken);
            }
        }

        if (buffer.Count > 0)
        {
            (int synced, int skipped) = await FlushSonarrSeriesChunkAsync(arrInstance, profileMap, buffer, now, cancellationToken);
            totalSynced += synced;
            totalSkipped += skipped;
            buffer.Clear();
            flushedChunks++;
        }

        _logger.LogTrace("[Sonarr] {InstanceName}: found {SeriesCount} total series",
            arrInstance.Name, totalSeries);

        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Sonarr, now);

        await CheckpointWalAsync(cancellationToken);

        _logger.LogInformation("[Sonarr] Synced CF scores for {Count} episodes on {InstanceName} ({Skipped} skipped)",
            totalSynced, arrInstance.Name, totalSkipped);
    }

    private async Task<(int Synced, int Skipped)> FlushSonarrSeriesChunkAsync(
        ArrInstance arrInstance,
        Dictionary<int, ArrQualityProfile> profileMap,
        List<SearchableSeries> chunk,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int synced = 0;
        int skipped = 0;

        SemaphoreSlim gate = new(FetchConcurrency);
        List<Task<SeriesFetch>> fetchTasks = new(chunk.Count);
        foreach (SearchableSeries series in chunk)
        {
            fetchTasks.Add(FetchSeriesAsync(arrInstance, series, gate, cancellationToken));
        }

        SeriesFetch[] fetched = await Task.WhenAll(fetchTasks);
        gate.Dispose();

        List<long> failedSeriesIds = fetched
            .Where(f => f.Failed)
            .Select(f => f.Series.Id)
            .ToList();

        // Collect all episodes with files for this chunk of series
        List<(SearchableSeries Series, SearchableEpisode Episode, long FileId, int CfScore, bool IsMonitored)> itemsInChunk = [];
        List<(long SeriesId, long EpisodeId)> episodesToTouch = [];

        foreach (SeriesFetch seriesFetch in fetched)
        {
            SearchableSeries series = seriesFetch.Series;
            List<SearchableEpisode> episodes = seriesFetch.Episodes;
            List<ArrEpisodeFile> episodeFiles = seriesFetch.EpisodeFiles;

            Dictionary<long, ArrEpisodeFile> fileMap = episodeFiles.ToDictionary(f => f.Id);

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
                    episodesToTouch.Add((series.Id, episode.Id));
                    if (episode.EpisodeFileId > 0)
                    {
                        skipped++;
                    }
                }
            }

            _logger.LogTrace("[Sonarr] {InstanceName}: series '{SeriesTitle}' (id={SeriesId}) has {TotalEpisodes} episodes, {FileCount} files, {Matched} matched",
                arrInstance.Name, series.Title, series.Id, episodes.Count, episodeFiles.Count, matched);
        }

        if (itemsInChunk.Count > 0)
        {
            List<long> seriesIds = itemsInChunk.Select(x => x.Series.Id).Distinct().ToList();
            Dictionary<(long, long), CustomFormatScoreEntry> existingEntries = await _eventsContext.CustomFormatScoreEntries
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == InstanceType.Sonarr
                    && seriesIds.Contains(e.ExternalItemId))
                .ToDictionaryAsync(e => (e.ExternalItemId, e.EpisodeId));

            bool autoDetect = _eventsContext.ChangeTracker.AutoDetectChangesEnabled;
            _eventsContext.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                foreach ((SearchableSeries series, SearchableEpisode episode, long fileId, int cfScore, bool isMonitored) in itemsInChunk)
                {
                    profileMap.TryGetValue(series.QualityProfileId, out ArrQualityProfile? profile);
                    int cutoffScore = profile?.CutoffFormatScore ?? 0;
                    string profileName = profile?.Name ?? "Unknown";

                    string title = $"{series.Title} S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";

                    existingEntries.TryGetValue((series.Id, episode.Id), out CustomFormatScoreEntry? existing);
                    UpsertCustomFormatScore(existing, arrInstance.Id, series.Id, episode.Id, InstanceType.Sonarr, title, fileId, cfScore, cutoffScore, profileName, isMonitored, now);

                    synced++;
                }

                _eventsContext.ChangeTracker.DetectChanges();
                await _eventsContext.SaveChangesAsync(cancellationToken);
                _eventsContext.ChangeTracker.Clear();
            }
            finally
            {
                _eventsContext.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
            }
        }
        else
        {
            _logger.LogTrace("[Sonarr] {InstanceName}: chunk of {ChunkSize} series yielded 0 episodes with files",
                arrInstance.Name, chunk.Count);
        }

        // Touch entries for episodes that exist but have no file (e.g., RSS upgrade in progress)
        foreach (var group in episodesToTouch.GroupBy(x => x.SeriesId))
        {
            List<long> episodeIds = group.Select(x => x.EpisodeId).ToList();
            foreach (long[] epChunk in episodeIds.Chunk(ChunkSize))
            {
                List<long> epChunkList = epChunk.ToList();
                await _eventsContext.CustomFormatScoreEntries
                    .Where(e => e.ArrInstanceId == arrInstance.Id
                        && e.ItemType == InstanceType.Sonarr
                        && e.ExternalItemId == group.Key
                        && epChunkList.Contains(e.EpisodeId))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSyncedAt, now), cancellationToken);
            }
        }

        if (failedSeriesIds.Count > 0)
        {
            foreach (long[] failedChunk in failedSeriesIds.Chunk(ChunkSize))
            {
                List<long> failedChunkList = failedChunk.ToList();
                await _eventsContext.CustomFormatScoreEntries
                    .Where(e => e.ArrInstanceId == arrInstance.Id
                        && e.ItemType == InstanceType.Sonarr
                        && failedChunkList.Contains(e.ExternalItemId))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSyncedAt, now), cancellationToken);
            }
        }

        return (synced, skipped);
    }

    private async Task<SeriesFetch> FetchSeriesAsync(
        ArrInstance arrInstance,
        SearchableSeries series,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Task<List<SearchableEpisode>> episodesTask = _sonarrClient.GetEpisodesAsync(arrInstance, series.Id, cancellationToken);
            Task<List<ArrEpisodeFile>> episodeFilesTask = _sonarrClient.GetEpisodeFilesAsync(arrInstance, series.Id, cancellationToken);
            await Task.WhenAll(episodesTask, episodeFilesTask);

            return new SeriesFetch(series, episodesTask.Result, episodeFilesTask.Result, Failed: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sonarr] Failed to fetch episodes for series '{SeriesTitle}' (id={SeriesId}) on {InstanceName}",
                series.Title, series.Id, arrInstance.Name);

            return new SeriesFetch(series, [], [], Failed: true);
        }
        finally
        {
            gate.Release();
        }
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
        DateTimeOffset now)
    {
        if (existing is not null)
        {
            if (existing.CurrentScore != cfScore)
            {
                _eventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
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

                if (cfScore > existing.CurrentScore)
                {
                    existing.LastUpgradedAt = now;
                }
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
            _eventsContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
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
            _eventsContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
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
        Guid arrInstanceId, InstanceType instanceType, DateTimeOffset syncStartTime)
    {
        List<long> staleItemIds = await _eventsContext.CustomFormatScoreEntries
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

        await _eventsContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstanceId
                && e.ItemType == instanceType
                && e.LastSyncedAt < syncStartTime)
            .ExecuteDeleteAsync();

        foreach (long[] chunk in staleItemIds.Chunk(ChunkSize))
        {
            List<long> chunkList = chunk.ToList();
            await _eventsContext.CustomFormatScoreHistory
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
        DateTimeOffset threshold = _timeProvider.GetUtcNow() - HistoryRetention;
        int deleted = await _eventsContext.CustomFormatScoreHistory
            .Where(h => h.RecordedAt < threshold)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogDebug("Cleaned up {Count} CF score history entries older than {Days} days",
                deleted, HistoryRetention.Days);
        }
    }
}
