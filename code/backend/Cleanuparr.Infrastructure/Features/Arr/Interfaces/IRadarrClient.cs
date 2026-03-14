using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface IRadarrClient : IArrClient
{
    /// <summary>
    /// Fetches all movies from a Radarr instance
    /// </summary>
    Task<List<SearchableMovie>> GetAllMoviesAsync(ArrInstance arrInstance);
}