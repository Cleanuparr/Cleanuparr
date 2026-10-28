using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface IRadarrClient : IArrClient
{
    /// <summary>
    /// Streams movies from a Radarr instance one item at a time
    /// </summary>
    IAsyncEnumerable<SearchableMovie> StreamAllMoviesAsync(ArrInstance arrInstance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches quality profiles from a Radarr instance
    /// </summary>
    Task<List<ArrQualityProfile>> GetQualityProfilesAsync(ArrInstance arrInstance);

    /// <summary>
    /// Fetches custom format scores for movie files in batches
    /// </summary>
    Task<Dictionary<long, int>> GetMovieFileScoresAsync(ArrInstance arrInstance, List<long> movieFileIds);

    /// <summary>
    /// Fetches the ids of movies Radarr grabbed since the given time.
    /// </summary>
    Task<HashSet<long>> GetRecentlyGrabbedMovieIdsAsync(ArrInstance arrInstance, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every movie Radarr lists as missing, filtered by monitored state.
    /// </summary>
    Task<List<SearchableMovie>> GetMissingMoviesAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every movie Radarr lists as a cutoff-unmet upgrade, filtered by monitored state.
    /// </summary>
    Task<List<SearchableMovie>> GetCutoffUnmetMoviesAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default);
}