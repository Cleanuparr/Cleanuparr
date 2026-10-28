namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// One track file of a Lidarr album.
/// </summary>
public sealed record ArrTrackFile
{
    /// <summary>
    /// The track file's id in Lidarr.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The album this file belongs to.
    /// </summary>
    public long AlbumId { get; init; }

    /// <summary>
    /// The file's custom format score.
    /// Lidarr scores an album by its lowest-id track file.
    /// </summary>
    public int CustomFormatScore { get; init; }
}
