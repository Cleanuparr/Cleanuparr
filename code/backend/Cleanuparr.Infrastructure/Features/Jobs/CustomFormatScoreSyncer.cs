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
/// Uses paged API fetching to handle large libraries efficiently.
/// </summary>
public sealed class CustomFormatScoreSyncer : IHandler
{
    private const int PageSize = 200;
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(120);

    private readonly ILogger<CustomFormatScoreSyncer> _logger;
    private readonly DataContext _dataContext;
    private readonly IRadarrClient _radarrClient;
    private readonly ISonarrClient _sonarrClient;

    public CustomFormatScoreSyncer(
        ILogger<CustomFormatScoreSyncer> logger,
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
        List<ArrQualityProfile> profiles = await _radarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        DateTime syncStartTime = DateTime.UtcNow;
        int page = 1;
        int totalSynced = 0;
        int processedRecords = 0;

        while (true)
        {
            PagedResponse<SearchableMovie> response = await _radarrClient.GetMoviesPagedAsync(arrInstance, page, PageSize);

            if (response.Records.Count == 0)
            {
                break;
            }

            List<(SearchableMovie Movie, long FileId)> moviesWithFiles = response.Records
                .Where(m => m.HasFile && m.MovieFile is not null)
                .Select(m => (Movie: m, FileId: m.MovieFile!.Id))
                .Where(x => x.FileId > 0)
                .ToList();

            if (moviesWithFiles.Count > 0)
            {
                List<long> fileIds = moviesWithFiles.Select(x => x.FileId).ToList();
                Dictionary<long, int> scores = await _radarrClient.GetMovieFileScoresAsync(arrInstance, fileIds);

                // Load existing entries for this page's movie IDs
                List<long> movieIds = moviesWithFiles.Select(x => x.Movie.Id).ToList();
                Dictionary<long, CustomFormatScoreEntry> existingEntries = await _dataContext.CustomFormatScoreEntries
                    .Where(e => e.ArrInstanceId == arrInstance.Id
                        && e.ItemType == InstanceType.Radarr
                        && movieIds.Contains(e.ExternalItemId))
                    .ToDictionaryAsync(e => e.ExternalItemId);

                foreach ((SearchableMovie movie, long fileId) in moviesWithFiles)
                {
                    if (!scores.TryGetValue(fileId, out int cfScore))
                    {
                        continue;
                    }

                    profileMap.TryGetValue(movie.QualityProfileId, out ArrQualityProfile? profile);
                    int cutoffScore = profile?.CutoffFormatScore ?? 0;
                    string profileName = profile?.Name ?? "Unknown";

                    existingEntries.TryGetValue(movie.Id, out CustomFormatScoreEntry? existing);
                    UpsertCustomFormatScore(existing, arrInstance.Id, movie.Id, 0, InstanceType.Radarr, movie.Title, fileId, cfScore, cutoffScore, profileName, syncStartTime);

                    totalSynced++;
                }

                await _dataContext.SaveChangesAsync();
            }

            processedRecords += response.Records.Count;

            if (processedRecords >= response.TotalRecords)
            {
                break;
            }

            page++;
            await Task.Delay(200);
        }

        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Radarr, syncStartTime);

        _logger.LogInformation("Synced CF scores for {Count} movies on {InstanceName}",
            totalSynced, arrInstance.Name);
    }

    private async Task SyncSonarrAsync(ArrInstance arrInstance)
    {
        List<ArrQualityProfile> profiles = await _sonarrClient.GetQualityProfilesAsync(arrInstance);
        Dictionary<int, ArrQualityProfile> profileMap = profiles.ToDictionary(p => p.Id);

        DateTime syncStartTime = DateTime.UtcNow;
        int page = 1;
        int totalSynced = 0;
        int processedRecords = 0;

        while (true)
        {
            PagedResponse<SearchableSeries> response = await _sonarrClient.GetSeriesPagedAsync(arrInstance, page, PageSize);

            if (response.Records.Count == 0)
            {
                break;
            }

            foreach (SearchableSeries series in response.Records)
            {
                try
                {
                    List<SearchableEpisode> episodes = await _sonarrClient.GetEpisodesAsync(arrInstance, series.Id);

                    List<(SearchableEpisode Episode, long FileId)> episodesWithFiles = episodes
                        .Where(e => e.HasFile && e.EpisodeFile is not null)
                        .Select(e => (Episode: e, FileId: e.EpisodeFile!.Id))
                        .Where(x => x.FileId > 0)
                        .ToList();

                    if (episodesWithFiles.Count == 0)
                    {
                        continue;
                    }

                    List<long> fileIds = episodesWithFiles.Select(x => x.FileId).ToList();
                    Dictionary<long, int> scores = await _sonarrClient.GetEpisodeFileScoresAsync(arrInstance, fileIds);

                    profileMap.TryGetValue(series.QualityProfileId, out ArrQualityProfile? profile);
                    int cutoffScore = profile?.CutoffFormatScore ?? 0;
                    string profileName = profile?.Name ?? "Unknown";

                    // Load existing entries for this series
                    Dictionary<(long, long), CustomFormatScoreEntry> existingEntries = await _dataContext.CustomFormatScoreEntries
                        .Where(e => e.ArrInstanceId == arrInstance.Id
                            && e.ItemType == InstanceType.Sonarr
                            && e.ExternalItemId == series.Id)
                        .ToDictionaryAsync(e => (e.ExternalItemId, e.EpisodeId));

                    foreach ((SearchableEpisode episode, long fileId) in episodesWithFiles)
                    {
                        if (!scores.TryGetValue(fileId, out int cfScore))
                        {
                            continue;
                        }

                        string title = $"{series.Title} S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";

                        existingEntries.TryGetValue((series.Id, episode.Id), out CustomFormatScoreEntry? existing);
                        UpsertCustomFormatScore(existing, arrInstance.Id, series.Id, episode.Id, InstanceType.Sonarr, title, fileId, cfScore, cutoffScore, profileName, syncStartTime);

                        totalSynced++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync CF scores for series {SeriesId} on {InstanceName}",
                        series.Id, arrInstance.Name);
                }

                // Rate limit to avoid overloading the Sonarr API
                await Task.Delay(Random.Shared.Next(500, 1500));
            }

            // Save per page to avoid large transactions
            await _dataContext.SaveChangesAsync();

            processedRecords += response.Records.Count;

            if (processedRecords >= response.TotalRecords)
            {
                break;
            }

            page++;
        }

        await CleanupStaleEntriesAsync(arrInstance.Id, InstanceType.Sonarr, syncStartTime);

        _logger.LogInformation("Synced CF scores for {Count} episodes on {InstanceName}",
            totalSynced, arrInstance.Name);
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
    /// Removes CF score entries not touched during this sync cycle (timestamp-based).
    /// </summary>
    private async Task CleanupStaleEntriesAsync(
        Guid arrInstanceId,
        InstanceType instanceType,
        DateTime syncStartTime)
    {
        List<CustomFormatScoreEntry> staleEntries = await _dataContext.CustomFormatScoreEntries
            .Where(e => e.ArrInstanceId == arrInstanceId
                && e.ItemType == instanceType
                && e.LastSyncedAt < syncStartTime)
            .ToListAsync();

        if (staleEntries.Count == 0)
        {
            return;
        }

        List<long> staleItemIds = staleEntries.Select(e => e.ExternalItemId).Distinct().ToList();

        _dataContext.CustomFormatScoreEntries.RemoveRange(staleEntries);

        // Also cleanup history for removed items
        await _dataContext.CustomFormatScoreHistory
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
