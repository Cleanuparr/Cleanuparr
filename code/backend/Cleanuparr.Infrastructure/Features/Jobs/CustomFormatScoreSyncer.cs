using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Persistence.Providers;
using Cleanuparr.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

/// <summary>
/// Periodically syncs custom format scores from Sonarr, Radarr, Lidarr and Whisparr v3 instances.
/// Tracks score changes over time for dashboard display and Seeker filtering.
/// </summary>
public sealed class CustomFormatScoreSyncer : IHandler
{
    private const int ChunkSize = 200;
    private const int CheckpointIntervalChunks = 10;
    private const int FetchConcurrency = 5;
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(120);

    private readonly ILogger<CustomFormatScoreSyncer> _logger;
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;
    private readonly ILidarrClient _lidarrClient;
    private readonly IWhisparrV2Client _whisparrV2Client;
    private readonly IWhisparrV3Client _whisparrV3Client;
    private readonly TimeProvider _timeProvider;
    private readonly IStatusNotifier _statusNotifier;
    private readonly IDatabaseProvider _databaseProvider;

    public CustomFormatScoreSyncer(
        ILogger<CustomFormatScoreSyncer> logger,
        DataContext dataContext,
        EventsContext eventsContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient,
        ILidarrClient lidarrClient,
        IWhisparrV2Client whisparrV2Client,
        IWhisparrV3Client whisparrV3Client,
        TimeProvider timeProvider,
        IStatusNotifier statusNotifier,
        IDatabaseProvider databaseProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
        _lidarrClient = lidarrClient;
        _whisparrV2Client = whisparrV2Client;
        _whisparrV3Client = whisparrV3Client;
        _timeProvider = timeProvider;
        _statusNotifier = statusNotifier;
        _databaseProvider = databaseProvider;
    }

    private async Task ConfigureSqliteCacheSizeAsync(CancellationToken cancellationToken)
    {
        await _databaseProvider.PrepareBulkWriteAsync(_eventsContext.Database, cancellationToken);
    }

    private async Task CheckpointWalIfDueAsync(int flushedChunks, CancellationToken cancellationToken)
    {
        if (flushedChunks % CheckpointIntervalChunks == 0)
        {
            await _databaseProvider.CheckpointAsync(_eventsContext.Database, cancellationToken);
        }
    }

    private async Task CheckpointWalAsync(CancellationToken cancellationToken)
    {
        await _databaseProvider.CheckpointAsync(_eventsContext.Database, cancellationToken);
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        List<SeekerInstanceConfig> instanceConfigs = await _dataContext.SeekerInstanceConfigs
            .Include(s => s.ArrInstance)
                .ThenInclude(a => a.ArrConfig)
            .Where(s => s.Enabled && s.ArrInstance.Enabled && s.UseCustomFormatScore)
            .ToListAsync(cancellationToken: cancellationToken);

        instanceConfigs = instanceConfigs
            .Where(s => SeekerApps.IsSupported(s.ArrInstance) && SeekerApps.ReadsCustomFormatScores(s.ArrInstance))
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

        await _statusNotifier.NotifyCustomFormatScoresUpdatedAsync(cancellationToken);
    }

    private async Task SyncInstanceAsync(ArrInstance arrInstance, CancellationToken cancellationToken)
    {
        InstanceType instanceType = arrInstance.ArrConfig.Type;

        _logger.LogDebug("Syncing CF scores for {InstanceType} instance: {InstanceName}",
            instanceType, arrInstance.Name);

        if (SeekerApps.SearchesMovies(arrInstance))
        {
            IRadarrClient movieClient = instanceType is InstanceType.Whisparr ? _whisparrV3Client : _radarrClient;
            await SyncMoviesAsync(arrInstance, movieClient, cancellationToken);
        }
        else if (instanceType is InstanceType.Lidarr)
        {
            await SyncLidarrAsync(arrInstance, cancellationToken);
        }
        else
        {
            ISonarrClient seriesClient = instanceType is InstanceType.Whisparr ? _whisparrV2Client : _sonarrClient;
            await SyncSonarrAsync(arrInstance, seriesClient, cancellationToken);
        }
    }

    private async Task SyncMoviesAsync(ArrInstance arrInstance, IRadarrClient movieClient, CancellationToken cancellationToken)
    {
        await ConfigureSqliteCacheSizeAsync(cancellationToken);

        InstanceType instanceType = arrInstance.ArrConfig.Type;
        List<ArrQualityProfile> profiles = await movieClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        _logger.LogTrace("[{InstanceType}] {InstanceName}: fetched {ProfileCount} quality profile(s)",
            instanceType, arrInstance.Name, profiles.Count);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        List<(SearchableMovie Movie, long FileId)> withFilesBuffer = new(ChunkSize);
        List<(long Id, bool Monitored)> withoutFilesBuffer = new(ChunkSize);

        int totalMovies = 0;
        int totalWithFiles = 0;
        int totalSynced = 0;
        int totalSkipped = 0;
        int flushedChunks = 0;

        await foreach (SearchableMovie movie in movieClient.StreamAllMoviesAsync(arrInstance, cancellationToken))
        {
            totalMovies++;

            if (movie.HasFile && movie.MovieFile is not null && movie.MovieFile.Id > 0)
            {
                withFilesBuffer.Add((movie, movie.MovieFile.Id));
                totalWithFiles++;

                if (withFilesBuffer.Count >= ChunkSize)
                {
                    (int synced, int skipped) = await FlushMovieWithFilesChunkAsync(arrInstance, movieClient, profileMap, withFilesBuffer, now);
                    totalSynced += synced;
                    totalSkipped += skipped;
                    withFilesBuffer.Clear();
                    flushedChunks++;
                    await CheckpointWalIfDueAsync(flushedChunks, cancellationToken);
                }
            }
            else
            {
                withoutFilesBuffer.Add((movie.Id, movie.Monitored));

                if (withoutFilesBuffer.Count >= ChunkSize)
                {
                    await FlushMovieTouchChunkAsync(arrInstance, withoutFilesBuffer, now);
                    withoutFilesBuffer.Clear();
                    flushedChunks++;
                    await CheckpointWalIfDueAsync(flushedChunks, cancellationToken);
                }
            }
        }

        if (withFilesBuffer.Count > 0)
        {
            (int synced, int skipped) = await FlushMovieWithFilesChunkAsync(arrInstance, movieClient, profileMap, withFilesBuffer, now);
            totalSynced += synced;
            totalSkipped += skipped;
            withFilesBuffer.Clear();
            flushedChunks++;
        }

        if (withoutFilesBuffer.Count > 0)
        {
            await FlushMovieTouchChunkAsync(arrInstance, withoutFilesBuffer, now);
            withoutFilesBuffer.Clear();
            flushedChunks++;
        }

        _logger.LogTrace("[{InstanceType}] {InstanceName}: found {TotalMovies} total movies, {WithFiles} with files",
            instanceType, arrInstance.Name, totalMovies, totalWithFiles);

        await CleanupStaleEntriesAsync(arrInstance.Id, instanceType, now);

        await CheckpointWalAsync(cancellationToken);

        _logger.LogInformation("[{InstanceType}] Synced CF scores for {Count} movies on {InstanceName} ({Skipped} skipped)",
            instanceType, totalSynced, arrInstance.Name, totalSkipped);
    }

    private async Task FlushMovieTouchChunkAsync(ArrInstance arrInstance, List<(long Id, bool Monitored)> movies, DateTimeOffset now)
    {
        InstanceType instanceType = arrInstance.ArrConfig.Type;
        foreach (IGrouping<bool, (long Id, bool Monitored)> group in movies.GroupBy(x => x.Monitored))
        {
            bool isMonitored = group.Key;
            List<long> chunkList = group.Select(x => x.Id).ToList();
            await _eventsContext.CustomFormatScoreEntries
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == instanceType
                    && chunkList.Contains(e.ExternalItemId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.LastSyncedAt, now)
                    .SetProperty(e => e.IsMonitored, isMonitored));
        }
    }

    private async Task<(int Synced, int Skipped)> FlushMovieWithFilesChunkAsync(
        ArrInstance arrInstance,
        IRadarrClient movieClient,
        Dictionary<int, ArrQualityProfile> profileMap,
        List<(SearchableMovie Movie, long FileId)> chunk,
        DateTimeOffset now)
    {
        InstanceType instanceType = arrInstance.ArrConfig.Type;
        int synced = 0;
        int skipped = 0;

        List<long> fileIds = chunk.Select(x => x.FileId).ToList();
        Dictionary<long, int> scores = await movieClient.GetMovieFileScoresAsync(arrInstance, fileIds);

        _logger.LogTrace("[{InstanceType}] {InstanceName}: chunk of {FileCount} file IDs returned {ScoreCount} score(s)",
            instanceType, arrInstance.Name, fileIds.Count, scores.Count);

        List<long> movieIds = chunk.Select(x => x.Movie.Id).ToList();
        Dictionary<long, CustomFormatScoreEntry> existingEntries = await _eventsContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstance.Id
                && e.ItemType == instanceType
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
                        skippedEntry.IsMonitored = movie.Monitored;
                    }
                    _logger.LogTrace("[{InstanceType}] {InstanceName}: skipping movie '{Title}' (fileId={FileId}), no score returned",
                        instanceType, arrInstance.Name, movie.Title, fileId);
                    continue;
                }

                profileMap.TryGetValue(movie.QualityProfileId, out ArrQualityProfile? profile);
                int cutoffScore = profile?.CutoffFormatScore ?? 0;
                string profileName = profile?.Name ?? "Unknown";

                existingEntries.TryGetValue(movie.Id, out CustomFormatScoreEntry? existing);
                UpsertCustomFormatScore(existing, arrInstance.Id, movie.Id, 0, instanceType, movie.Title, fileId, cfScore, cutoffScore, profileName, movie.Monitored, now, seasonNumber: null, airDateUtc: null, lastSearchTime: null);

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

    private async Task SyncSonarrAsync(ArrInstance arrInstance, ISonarrClient seriesClient, CancellationToken cancellationToken)
    {
        await ConfigureSqliteCacheSizeAsync(cancellationToken);

        InstanceType instanceType = arrInstance.ArrConfig.Type;
        List<ArrQualityProfile> profiles = await seriesClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        _logger.LogTrace("[{InstanceType}] {InstanceName}: fetched {ProfileCount} quality profile(s)",
            instanceType, arrInstance.Name, profiles.Count);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        List<SearchableSeries> buffer = new(ChunkSize);

        int totalSeries = 0;
        int totalSynced = 0;
        int totalSkipped = 0;
        int flushedChunks = 0;

        await foreach (SearchableSeries series in seriesClient.StreamAllSeriesAsync(arrInstance, cancellationToken))
        {
            totalSeries++;
            buffer.Add(series);

            if (buffer.Count >= ChunkSize)
            {
                (int synced, int skipped) = await FlushSonarrSeriesChunkAsync(arrInstance, seriesClient, profileMap, buffer, now, cancellationToken);
                totalSynced += synced;
                totalSkipped += skipped;
                buffer.Clear();
                flushedChunks++;
                await CheckpointWalIfDueAsync(flushedChunks, cancellationToken);
            }
        }

        if (buffer.Count > 0)
        {
            (int synced, int skipped) = await FlushSonarrSeriesChunkAsync(arrInstance, seriesClient, profileMap, buffer, now, cancellationToken);
            totalSynced += synced;
            totalSkipped += skipped;
            buffer.Clear();
            flushedChunks++;
        }

        _logger.LogTrace("[{InstanceType}] {InstanceName}: found {SeriesCount} total series",
            instanceType, arrInstance.Name, totalSeries);

        await CleanupStaleEntriesAsync(arrInstance.Id, instanceType, now);

        await CheckpointWalAsync(cancellationToken);

        _logger.LogInformation("[{InstanceType}] Synced CF scores for {Count} episodes on {InstanceName} ({Skipped} skipped)",
            instanceType, totalSynced, arrInstance.Name, totalSkipped);
    }

    private async Task<(int Synced, int Skipped)> FlushSonarrSeriesChunkAsync(
        ArrInstance arrInstance,
        ISonarrClient seriesClient,
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
            fetchTasks.Add(FetchSeriesAsync(arrInstance, seriesClient, series, gate, cancellationToken));
        }

        SeriesFetch[] fetched = await Task.WhenAll(fetchTasks);
        gate.Dispose();

        List<long> failedSeriesIds = fetched
            .Where(f => f.Failed)
            .Select(f => f.Series.Id)
            .ToList();

        // Collect all episodes with files for this chunk of series
        List<(SearchableSeries Series, SearchableEpisode Episode, long FileId, int CfScore, bool IsMonitored)> itemsInChunk = [];
        List<(long SeriesId, long EpisodeId, bool IsMonitored)> episodesToTouch = [];

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
                    episodesToTouch.Add((series.Id, episode.Id, series.Monitored && episode.Monitored));
                    if (episode.EpisodeFileId > 0)
                    {
                        skipped++;
                    }
                }
            }

            _logger.LogTrace("[{InstanceType}] {InstanceName}: series '{SeriesTitle}' (id={SeriesId}) has {TotalEpisodes} episodes, {FileCount} files, {Matched} matched",
                arrInstance.ArrConfig.Type, arrInstance.Name, series.Title, series.Id, episodes.Count, episodeFiles.Count, matched);
        }

        if (itemsInChunk.Count > 0)
        {
            List<long> seriesIds = itemsInChunk.Select(x => x.Series.Id).Distinct().ToList();
            Dictionary<(long, long), CustomFormatScoreEntry> existingEntries = await _eventsContext.CustomFormatScoreEntries
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == arrInstance.ArrConfig.Type
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

                    string title = SeekerApps.EpisodeName(arrInstance, series.Title, episode.SeasonNumber, episode.EpisodeNumber, episode.AirDateUtc);

                    existingEntries.TryGetValue((series.Id, episode.Id), out CustomFormatScoreEntry? existing);
                    UpsertCustomFormatScore(existing, arrInstance.Id, series.Id, episode.Id, arrInstance.ArrConfig.Type, title, fileId, cfScore, cutoffScore, profileName, isMonitored, now, episode.SeasonNumber, episode.AirDateUtc, episode.LastSearchTime);

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
            _logger.LogTrace("[{InstanceType}] {InstanceName}: chunk of {ChunkSize} series yielded 0 episodes with files",
                arrInstance.ArrConfig.Type, arrInstance.Name, chunk.Count);
        }

        // Touch entries for episodes that exist but have no file (e.g., RSS upgrade in progress)
        foreach (IGrouping<(long SeriesId, bool IsMonitored), (long SeriesId, long EpisodeId, bool IsMonitored)> group in episodesToTouch.GroupBy(x => (x.SeriesId, x.IsMonitored)))
        {
            (long seriesId, bool isMonitored) = group.Key;
            List<long> episodeIds = group.Select(x => x.EpisodeId).ToList();
            foreach (long[] epChunk in episodeIds.Chunk(ChunkSize))
            {
                List<long> epChunkList = epChunk.ToList();
                await _eventsContext.CustomFormatScoreEntries
                    .Where(e => e.ArrInstanceId == arrInstance.Id
                        && e.ItemType == arrInstance.ArrConfig.Type
                        && e.ExternalItemId == seriesId
                        && epChunkList.Contains(e.EpisodeId))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.LastSyncedAt, now)
                        .SetProperty(e => e.IsMonitored, isMonitored), cancellationToken);
            }
        }

        if (failedSeriesIds.Count > 0)
        {
            foreach (long[] failedChunk in failedSeriesIds.Chunk(ChunkSize))
            {
                List<long> failedChunkList = failedChunk.ToList();
                await _eventsContext.CustomFormatScoreEntries
                    .Where(e => e.ArrInstanceId == arrInstance.Id
                        && e.ItemType == arrInstance.ArrConfig.Type
                        && failedChunkList.Contains(e.ExternalItemId))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSyncedAt, now), cancellationToken);
            }
        }

        return (synced, skipped);
    }

    private async Task<SeriesFetch> FetchSeriesAsync(
        ArrInstance arrInstance,
        ISonarrClient seriesClient,
        SearchableSeries series,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Task<List<SearchableEpisode>> episodesTask = seriesClient.GetEpisodesAsync(arrInstance, series.Id, cancellationToken);
            Task<List<ArrEpisodeFile>> episodeFilesTask = seriesClient.GetEpisodeFilesAsync(arrInstance, series.Id, cancellationToken);
            await Task.WhenAll(episodesTask, episodeFilesTask);

            return new SeriesFetch(series, episodesTask.Result, episodeFilesTask.Result, Failed: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{InstanceType}] Failed to fetch episodes for series '{SeriesTitle}' (id={SeriesId}) on {InstanceName}",
                arrInstance.ArrConfig.Type, series.Title, series.Id, arrInstance.Name);

            return new SeriesFetch(series, [], [], Failed: true);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task SyncLidarrAsync(ArrInstance arrInstance, CancellationToken cancellationToken)
    {
        await ConfigureSqliteCacheSizeAsync(cancellationToken);

        List<ArrQualityProfile> profiles = await _lidarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        _logger.LogTrace("[Lidarr] {InstanceName}: fetched {ProfileCount} quality profile(s)",
            arrInstance.Name, profiles.Count);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        List<SearchableArtist> buffer = new(ChunkSize);

        int totalArtists = 0;
        int totalSynced = 0;
        int totalSkipped = 0;
        int flushedChunks = 0;

        await foreach (SearchableArtist artist in _lidarrClient.StreamAllArtistsAsync(arrInstance, cancellationToken))
        {
            totalArtists++;
            buffer.Add(artist);

            if (buffer.Count >= ChunkSize)
            {
                (int synced, int skipped) = await FlushLidarrArtistChunkAsync(arrInstance, profileMap, buffer, now, cancellationToken);
                totalSynced += synced;
                totalSkipped += skipped;
                buffer.Clear();
                flushedChunks++;
                await CheckpointWalIfDueAsync(flushedChunks, cancellationToken);
            }
        }

        if (buffer.Count > 0)
        {
            (int synced, int skipped) = await FlushLidarrArtistChunkAsync(arrInstance, profileMap, buffer, now, cancellationToken);
            totalSynced += synced;
            totalSkipped += skipped;
            buffer.Clear();
        }

        _logger.LogTrace("[Lidarr] {InstanceName}: found {ArtistCount} total artists",
            arrInstance.Name, totalArtists);

        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Lidarr, now);

        await CheckpointWalAsync(cancellationToken);

        _logger.LogInformation("[Lidarr] Synced CF scores for {Count} albums on {InstanceName} ({Skipped} skipped)",
            totalSynced, arrInstance.Name, totalSkipped);
    }

    private async Task<(int Synced, int Skipped)> FlushLidarrArtistChunkAsync(
        ArrInstance arrInstance,
        Dictionary<int, ArrQualityProfile> profileMap,
        List<SearchableArtist> chunk,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int synced = 0;
        int skipped = 0;

        SemaphoreSlim gate = new(FetchConcurrency);
        List<Task<ArtistFetch>> fetchTasks = new(chunk.Count);
        foreach (SearchableArtist artist in chunk)
        {
            fetchTasks.Add(FetchArtistAsync(arrInstance, artist, gate, cancellationToken));
        }

        ArtistFetch[] fetched = await Task.WhenAll(fetchTasks);
        gate.Dispose();

        List<long> failedArtistIds = fetched
            .Where(f => f.Failed)
            .Select(f => f.Artist.Id)
            .ToList();

        List<(SearchableArtist Artist, SearchableAlbum Album, ArrTrackFile File)> itemsInChunk = [];
        List<(long ArtistId, long AlbumId, bool IsMonitored)> albumsToTouch = [];

        foreach (ArtistFetch artistFetch in fetched)
        {
            SearchableArtist artist = artistFetch.Artist;

            int matched = 0;
            foreach (SearchableAlbum album in artistFetch.Albums)
            {
                bool hasAllTracks = album.Statistics is { TrackFileCount: > 0 } stats && stats.TrackFileCount >= stats.TrackCount;

                // Lidarr scores an album by its first track file
                ArrTrackFile? first = hasAllTracks
                    ? artistFetch.TrackFiles.Where(f => f.AlbumId == album.Id).MinBy(f => f.Id)
                    : null;

                if (first is not null)
                {
                    itemsInChunk.Add((artist, album, first));
                    matched++;
                }
                else
                {
                    albumsToTouch.Add((artist.Id, album.Id, artist.Monitored && album.Monitored));
                    if (hasAllTracks)
                    {
                        skipped++;
                    }
                }
            }

            _logger.LogTrace("[Lidarr] {InstanceName}: artist '{ArtistName}' (id={ArtistId}) has {TotalAlbums} albums, {FileCount} track files, {Matched} matched",
                arrInstance.Name, artist.ArtistName, artist.Id, artistFetch.Albums.Count, artistFetch.TrackFiles.Count, matched);
        }

        if (itemsInChunk.Count > 0)
        {
            List<long> artistIds = itemsInChunk.Select(x => x.Artist.Id).Distinct().ToList();
            Dictionary<(long, long), CustomFormatScoreEntry> existingEntries = await _eventsContext.CustomFormatScoreEntries
                .Where(e => e.ArrInstanceId == arrInstance.Id
                    && e.ItemType == InstanceType.Lidarr
                    && artistIds.Contains(e.ExternalItemId))
                .ToDictionaryAsync(e => (e.ExternalItemId, e.EpisodeId), cancellationToken);

            bool autoDetect = _eventsContext.ChangeTracker.AutoDetectChangesEnabled;
            _eventsContext.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                foreach ((SearchableArtist artist, SearchableAlbum album, ArrTrackFile file) in itemsInChunk)
                {
                    profileMap.TryGetValue(artist.QualityProfileId, out ArrQualityProfile? profile);
                    int cutoffScore = profile?.CutoffFormatScore ?? 0;
                    string profileName = profile?.Name ?? "Unknown";

                    string title = $"{artist.ArtistName} - {album.Title}";

                    existingEntries.TryGetValue((artist.Id, album.Id), out CustomFormatScoreEntry? existing);
                    UpsertCustomFormatScore(existing, arrInstance.Id, artist.Id, album.Id, InstanceType.Lidarr, title, file.Id, file.CustomFormatScore, cutoffScore, profileName, artist.Monitored && album.Monitored, now, seasonNumber: 0, airDateUtc: album.ReleaseDate, lastSearchTime: album.LastSearchTime);

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
            _logger.LogTrace("[Lidarr] {InstanceName}: chunk of {ChunkSize} artists yielded 0 albums with track files",
                arrInstance.Name, chunk.Count);
        }

        // Keeps partial albums out of stale cleanup
        foreach (IGrouping<(long ArtistId, bool IsMonitored), (long ArtistId, long AlbumId, bool IsMonitored)> group in albumsToTouch.GroupBy(x => (x.ArtistId, x.IsMonitored)))
        {
            (long artistId, bool isMonitored) = group.Key;
            List<long> albumIds = group.Select(x => x.AlbumId).ToList();
            foreach (long[] albumChunk in albumIds.Chunk(ChunkSize))
            {
                List<long> albumChunkList = albumChunk.ToList();
                await _eventsContext.CustomFormatScoreEntries
                    .Where(e => e.ArrInstanceId == arrInstance.Id
                        && e.ItemType == InstanceType.Lidarr
                        && e.ExternalItemId == artistId
                        && albumChunkList.Contains(e.EpisodeId))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.LastSyncedAt, now)
                        .SetProperty(e => e.IsMonitored, isMonitored), cancellationToken);
            }
        }

        if (failedArtistIds.Count > 0)
        {
            foreach (long[] failedChunk in failedArtistIds.Chunk(ChunkSize))
            {
                List<long> failedChunkList = failedChunk.ToList();
                await _eventsContext.CustomFormatScoreEntries
                    .Where(e => e.ArrInstanceId == arrInstance.Id
                        && e.ItemType == InstanceType.Lidarr
                        && failedChunkList.Contains(e.ExternalItemId))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastSyncedAt, now), cancellationToken);
            }
        }

        return (synced, skipped);
    }

    private async Task<ArtistFetch> FetchArtistAsync(
        ArrInstance arrInstance,
        SearchableArtist artist,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Task<List<SearchableAlbum>> albumsTask = _lidarrClient.GetAlbumsAsync(arrInstance, artist.Id, cancellationToken);
            Task<List<ArrTrackFile>> trackFilesTask = _lidarrClient.GetTrackFilesAsync(arrInstance, artist.Id, cancellationToken);
            await Task.WhenAll(albumsTask, trackFilesTask);

            return new ArtistFetch(artist, albumsTask.Result, trackFilesTask.Result, Failed: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Lidarr] Failed to fetch albums for artist '{ArtistName}' (id={ArtistId}) on {InstanceName}",
                artist.ArtistName, artist.Id, arrInstance.Name);
            return new ArtistFetch(artist, [], [], Failed: true);
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
        DateTimeOffset now,
        int? seasonNumber,
        DateTimeOffset? airDateUtc,
        DateTimeOffset? lastSearchTime)
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
            existing.SeasonNumber = seasonNumber;
            existing.AirDateUtc = airDateUtc;
            existing.LastSearchTime = lastSearchTime;
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
                SeasonNumber = seasonNumber,
                AirDateUtc = airDateUtc,
                LastSearchTime = lastSearchTime,
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
        int deletedEntries = await _eventsContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstanceId
                && e.ItemType == instanceType
                && e.LastSyncedAt < syncStartTime)
            .ExecuteDeleteAsync();

        if (deletedEntries == 0)
        {
            return;
        }

        // Match on EpisodeId too, so a removed album or episode keeps its siblings' history
        await _eventsContext.CustomFormatScoreHistory
            .Where(h => h.ArrInstanceId == arrInstanceId
                && h.ItemType == instanceType
                && !_eventsContext.CustomFormatScoreEntries.Any(e => e.ArrInstanceId == h.ArrInstanceId
                    && e.ItemType == h.ItemType
                    && e.ExternalItemId == h.ExternalItemId
                    && e.EpisodeId == h.EpisodeId))
            .ExecuteDeleteAsync();

        _logger.LogTrace("Cleaned up {Count} stale CF score entries for instance {InstanceId}",
            deletedEntries, arrInstanceId);
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
