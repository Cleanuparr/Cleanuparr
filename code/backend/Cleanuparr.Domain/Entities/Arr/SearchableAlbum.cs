namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// A Lidarr album the Seeker can search for.
/// </summary>
public sealed record SearchableAlbum
{
    /// <summary>
    /// The album's id in Lidarr.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The album's title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The artist who owns this album.
    /// </summary>
    public long ArtistId { get; init; }

    /// <summary>
    /// Whether Lidarr monitors this album.
    /// </summary>
    public bool Monitored { get; init; }

    /// <summary>
    /// The album's release date.
    /// Null when Lidarr has none.
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; init; }

    /// <summary>
    /// Set when a search reached an indexer; RSS grabs leave it alone.
    /// </summary>
    public DateTimeOffset? LastSearchTime { get; init; }

    /// <summary>
    /// The album's track file counts.
    /// Null when Lidarr omits them.
    /// </summary>
    public AlbumStatistics? Statistics { get; init; }
}
