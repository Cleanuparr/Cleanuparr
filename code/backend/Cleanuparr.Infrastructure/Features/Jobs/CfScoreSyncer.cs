using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

/// <summary>
/// Periodically syncs custom format scores from Radarr/Sonarr instances.
/// Tracks score changes over time for dashboard display and Seeker filtering.
/// </summary>
public sealed class CfScoreSyncer : IHandler
{
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(120);

    private readonly ILogger<CfScoreSyncer> _logger;
    private readonly DataContext _dataContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;

    public CfScoreSyncer(
        ILogger<CfScoreSyncer> logger,
        DataContext dataContext,
        IRadarrClient radarrClient,
        ISonarrClient sonarrClient)
    {
        _logger = logger;
        _dataContext = dataContext;
        _radarrClient = radarrClient;
        _sonarrClient = sonarrClient;
    }

    public async Task ExecuteAsync()
    {
        SeekerConfig config = await _dataContext.SeekerConfigs
            .AsNoTracking()
            .FirstAsync();

        if (!config.UseCustomFormatScore)
        {
            _logger.LogDebug("Custom format score tracking is disabled");
            return;
        }

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
            _logger.LogDebug("No enabled instances for CF score sync");
            return;
        }

        foreach (SeekerInstanceConfig instanceConfig in instanceConfigs)
        {
            try
            {
                await SyncInstanceAsync(instanceConfig.ArrInstance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync CF scores for {InstanceName}",
                    instanceConfig.ArrInstance.Name);
            }
        }

        await CleanupOldHistoryAsync();
    }

    private async Task SyncInstanceAsync(ArrInstance arrInstance)
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
            await SyncSonarrAsync(arrInstance);
        }
    }

    private async Task SyncRadarrAsync(ArrInstance arrInstance)
    {
        // Fetch quality profiles
        List<ArrQualityProfile> profiles = await _radarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        // Fetch all movies
        List<SearchableMovie> movies = await _radarrClient.GetAllMoviesAsync(arrInstance);

        // Extract file IDs from movies that have files
        List<(SearchableMovie Movie, long FileId)> moviesWithFiles = movies
            .Where(m => m.HasFile && m.MovieFile is not null)
            .Select(m => (Movie: m, FileId: m.MovieFile!.Id))
            .Where(x => x.FileId > 0)
            .ToList();

        if (moviesWithFiles.Count == 0)
        {
            _logger.LogDebug("No movies with files to sync on {InstanceName}", arrInstance.Name);
            await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Radarr, []);
            return;
        }

        // Batch-fetch CF scores
        List<long> fileIds = moviesWithFiles.Select(x => x.FileId).ToList();
        Dictionary<long, int> scores = await _radarrClient.GetMovieFileScoresAsync(arrInstance, fileIds);

        // Load existing entries for this instance
        Dictionary<long, CfScoreEntry> existingEntries = await _dataContext.CfScoreEntries
            .Where(e => e.ArrInstanceId == arrInstance.Id && e.ItemType == InstanceType.Radarr)
            .ToDictionaryAsync(e => e.ExternalItemId);

        DateTime now = DateTime.UtcNow;
        List<long> syncedItemIds = [];

        foreach ((SearchableMovie movie, long fileId) in moviesWithFiles)
        {
            if (!scores.TryGetValue(fileId, out int cfScore))
            {
                continue;
            }

            profileMap.TryGetValue(movie.QualityProfileId, out ArrQualityProfile? profile);
            int cutoffScore = profile?.CutoffFormatScore ?? 0;
            string profileName = profile?.Name ?? "Unknown";

            syncedItemIds.Add(movie.Id);

            existingEntries.TryGetValue(movie.Id, out CfScoreEntry? existing);
            UpsertCfScore(existing, arrInstance.Id, movie.Id, 0, InstanceType.Radarr, movie.Title, fileId, cfScore, cutoffScore, profileName, now);
        }

        await _dataContext.SaveChangesAsync();
        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Radarr, syncedItemIds);

        _logger.LogInformation("Synced CF scores for {Count} movies on {InstanceName}",
            syncedItemIds.Count, arrInstance.Name);
    }

    private async Task SyncSonarrAsync(ArrInstance arrInstance)
    {
        // Fetch quality profiles
        List<ArrQualityProfile> profiles = await _sonarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        // Fetch all series
        List<SearchableSeries> allSeries = await _sonarrClient.GetAllSeriesAsync(arrInstance);

        // Load existing entries for this instance
        Dictionary<(long, long), CfScoreEntry> existingEntries = await _dataContext.CfScoreEntries
            .Where(e => e.ArrInstanceId == arrInstance.Id && e.ItemType == InstanceType.Sonarr)
            .ToDictionaryAsync(e => (e.ExternalItemId, e.EpisodeId));

        DateTime now = DateTime.UtcNow;
        List<long> syncedSeriesIds = [];
        int totalSynced = 0;

        foreach (SearchableSeries series in allSeries)
        {
            try
            {
                List<SearchableEpisode> episodes = await _sonarrClient.GetEpisodesAsync(arrInstance, series.Id);

                // Extract file IDs from episodes that have files
                List<(SearchableEpisode Episode, long FileId)> episodesWithFiles = episodes
                    .Where(e => e.HasFile && e.EpisodeFile is not null)
                    .Select(e => (Episode: e, FileId: e.EpisodeFile!.Id))
                    .Where(x => x.FileId > 0)
                    .ToList();

                if (episodesWithFiles.Count == 0)
                {
                    continue;
                }

                // Batch-fetch CF scores for this series' episodes
                List<long> fileIds = episodesWithFiles.Select(x => x.FileId).ToList();
                Dictionary<long, int> scores = await _sonarrClient.GetEpisodeFileScoresAsync(arrInstance, fileIds);

                profileMap.TryGetValue(series.QualityProfileId, out ArrQualityProfile? profile);
                int cutoffScore = profile?.CutoffFormatScore ?? 0;
                string profileName = profile?.Name ?? "Unknown";

                syncedSeriesIds.Add(series.Id);

                foreach ((SearchableEpisode episode, long fileId) in episodesWithFiles)
                {
                    if (!scores.TryGetValue(fileId, out int cfScore))
                    {
                        continue;
                    }

                    string title = $"{series.Title} S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";

                    existingEntries.TryGetValue((series.Id, episode.Id), out CfScoreEntry? existing);
                    UpsertCfScore(existing, arrInstance.Id, series.Id, episode.Id, InstanceType.Sonarr, title, fileId, cfScore, cutoffScore, profileName, now);

                    totalSynced++;
                }

                // Save per-series to avoid large transactions
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync CF scores for series {SeriesId} on {InstanceName}",
                    series.Id, arrInstance.Name);
            }

            // Rate limit to avoid overloading the Sonarr API
            await Task.Delay(Random.Shared.Next(500, 1500));
        }

        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Sonarr, syncedSeriesIds);

        _logger.LogInformation("Synced CF scores for {Count} episodes across {SeriesCount} series on {InstanceName}",
            totalSynced, syncedSeriesIds.Count, arrInstance.Name);
    }

    /// <summary>
    /// Creates or updates a CF score entry and records history when the score changes.
    /// </summary>
    private void UpsertCfScore(
        CfScoreEntry? existing,
        Guid arrInstanceId,
        long externalItemId,
        long episodeId,
        InstanceType itemType,
        string title,
        long fileId,
        int cfScore,
        int cutoffScore,
        string profileName,
        DateTime now)
    {
        if (existing is not null)
        {
            if (existing.CurrentScore != cfScore)
            {
                _dataContext.CfScoreHistory.Add(new CfScoreHistory
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
            existing.LastSyncedAt = now;
        }
        else
        {
            _dataContext.CfScoreEntries.Add(new CfScoreEntry
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
                LastSyncedAt = now,
            });

            // Record initial score in history
            _dataContext.CfScoreHistory.Add(new CfScoreHistory
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
    /// Removes CF score entries for items no longer in the library
    /// </summary>
    private async Task CleanupStaleEntriesAsync(
        Guid arrInstanceId,
        InstanceType instanceType,
        List<long> currentItemIds)
    {
        List<CfScoreEntry> staleEntries = await _dataContext.CfScoreEntries
            .Where(e => e.ArrInstanceId == arrInstanceId
                && e.ItemType == instanceType
                && !currentItemIds.Contains(e.ExternalItemId))
            .ToListAsync();

        if (staleEntries.Count == 0)
        {
            return;
        }

        List<long> staleItemIds = staleEntries.Select(e => e.ExternalItemId).Distinct().ToList();

        _dataContext.CfScoreEntries.RemoveRange(staleEntries);

        // Also cleanup history for removed items
        await _dataContext.CfScoreHistory
            .Where(h => h.ArrInstanceId == arrInstanceId
                && h.ItemType == instanceType
                && staleItemIds.Contains(h.ExternalItemId))
            .ExecuteDeleteAsync();

        await _dataContext.SaveChangesAsync();

        _logger.LogDebug("Cleaned up {Count} stale CF score entries for instance {InstanceId}",
            staleEntries.Count, arrInstanceId);
    }

    /// <summary>
    /// Removes CF score history entries older than the retention period
    /// </summary>
    private async Task CleanupOldHistoryAsync()
    {
        DateTime threshold = DateTime.UtcNow - HistoryRetention;
        int deleted = await _dataContext.CfScoreHistory
            .Where(h => h.RecordedAt < threshold)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogDebug("Cleaned up {Count} CF score history entries older than {Days} days",
                deleted, HistoryRetention.Days);
        }
    }
}
