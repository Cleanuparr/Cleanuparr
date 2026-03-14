using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface ISonarrClient : IArrClient
{
    /// <summary>
    /// Fetches all series from a Sonarr instance
    /// </summary>
    Task<List<SearchableSeries>> GetAllSeriesAsync(ArrInstance arrInstance);

    /// <summary>
    /// Fetches all episodes for a specific series from a Sonarr instance
    /// </summary>
    Task<List<SearchableEpisode>> GetEpisodesAsync(ArrInstance arrInstance, long seriesId);
}