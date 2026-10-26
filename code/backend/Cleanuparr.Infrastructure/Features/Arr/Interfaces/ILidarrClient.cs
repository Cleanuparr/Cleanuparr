using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface ILidarrClient : IArrClient
{
    /// <summary>
    /// Streams artists from a Lidarr instance one item at a time
    /// </summary>
    IAsyncEnumerable<SearchableArtist> StreamAllArtistsAsync(ArrInstance arrInstance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches quality profiles from a Lidarr instance
    /// </summary>
    Task<List<ArrQualityProfile>> GetQualityProfilesAsync(ArrInstance arrInstance);

    /// <summary>
    /// Fetches all albums for a specific artist from a Lidarr instance
    /// </summary>
    Task<List<SearchableAlbum>> GetAlbumsAsync(ArrInstance arrInstance, long artistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the track files of a specific artist, including their custom format scores
    /// </summary>
    Task<List<ArrTrackFile>> GetTrackFilesAsync(ArrInstance arrInstance, long artistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the ids of albums Lidarr grabbed since the given time.
    /// </summary>
    Task<HashSet<long>> GetRecentlyGrabbedAlbumIdsAsync(ArrInstance arrInstance, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every album Lidarr lists as missing, filtered by monitored state.
    /// </summary>
    Task<List<SearchableAlbum>> GetMissingAlbumsAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every album Lidarr lists as a cutoff-unmet upgrade, filtered by monitored state.
    /// </summary>
    Task<List<SearchableAlbum>> GetCutoffUnmetAlbumsAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default);
}
