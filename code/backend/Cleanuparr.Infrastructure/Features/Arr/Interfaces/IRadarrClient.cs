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
}