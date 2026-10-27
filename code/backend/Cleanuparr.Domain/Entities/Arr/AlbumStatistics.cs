namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// Lidarr's file counts for one album.
/// </summary>
public sealed record AlbumStatistics
{
    /// <summary>
    /// Tracks of the album that have a file on disk.
    /// </summary>
    public int TrackFileCount { get; init; }

    /// <summary>
    /// Tracks the album has in total.
    /// The album is complete when TrackFileCount reaches it.
    /// </summary>
    public int TrackCount { get; init; }
}
