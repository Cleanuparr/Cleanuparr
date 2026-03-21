using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface IRadarrClient : IArrClient
{
    /// <summary>
    /// Fetches all movies from a Radarr instance
    /// </summary>
    Task<List<SearchableMovie>> GetAllMoviesAsync(ArrInstance arrInstance);

    /// <summary>
    /// Fetches quality profiles from a Radarr instance
    /// </summary>
    Task<List<ArrQualityProfile>> GetQualityProfilesAsync(ArrInstance arrInstance);

    /// <summary>
    /// Fetches custom format scores for movie files in batches
    /// </summary>
    Task<Dictionary<long, int>> GetMovieFileScoresAsync(ArrInstance arrInstance, List<long> movieFileIds);
}