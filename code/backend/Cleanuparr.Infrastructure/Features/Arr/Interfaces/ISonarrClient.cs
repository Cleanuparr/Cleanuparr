using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface ISonarrClient : IArrClient
{
    /// <summary>
    /// Streams series from a Sonarr instance one item at a time
    /// </summary>
    IAsyncEnumerable<SearchableSeries> StreamAllSeriesAsync(ArrInstance arrInstance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all episodes for a specific series from a Sonarr instance
    /// </summary>
    Task<List<SearchableEpisode>> GetEpisodesAsync(ArrInstance arrInstance, long seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches quality profiles from a Sonarr instance
    /// </summary>
    Task<List<ArrQualityProfile>> GetQualityProfilesAsync(ArrInstance arrInstance);

    /// <summary>
    /// Fetches episode file metadata for a specific series, including quality cutoff status
    /// </summary>
    Task<List<ArrEpisodeFile>> GetEpisodeFilesAsync(ArrInstance arrInstance, long seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches custom format scores for episode files in batches
    /// </summary>
    Task<Dictionary<long, int>> GetEpisodeFileScoresAsync(ArrInstance arrInstance, List<long> episodeFileIds);

    /// <summary>
    /// Fetches the episodes Sonarr grabbed since the given time, with their series and season.
    /// </summary>
    Task<HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)>> GetRecentlyGrabbedEpisodesAsync(ArrInstance arrInstance, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every episode Sonarr lists as missing, filtered by monitored state.
    /// </summary>
    Task<List<SearchableEpisode>> GetMissingEpisodesAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every episode Sonarr lists as a cutoff-unmet upgrade, filtered by monitored state.
    /// </summary>
    Task<List<SearchableEpisode>> GetCutoffUnmetEpisodesAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default);
}