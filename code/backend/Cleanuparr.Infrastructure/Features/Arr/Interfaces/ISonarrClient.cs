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
    Task<List<SearchableEpisode>> GetEpisodesAsync(ArrInstance arrInstance, long seriesId);

    /// <summary>
    /// Fetches quality profiles from a Sonarr instance
    /// </summary>
    Task<List<ArrQualityProfile>> GetQualityProfilesAsync(ArrInstance arrInstance);

    /// <summary>
    /// Fetches episode file metadata for a specific series, including quality cutoff status
    /// </summary>
    Task<List<ArrEpisodeFile>> GetEpisodeFilesAsync(ArrInstance arrInstance, long seriesId);

    /// <summary>
    /// Fetches custom format scores for episode files in batches
    /// </summary>
    Task<Dictionary<long, int>> GetEpisodeFileScoresAsync(ArrInstance arrInstance, List<long> episodeFileIds);
}