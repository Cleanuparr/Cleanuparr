namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// A Lidarr artist the Seeker can search for.
/// </summary>
public sealed record SearchableArtist
{
    /// <summary>
    /// The artist's id in Lidarr.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The artist's name.
    /// </summary>
    public string ArtistName { get; init; } = string.Empty;

    /// <summary>
    /// Whether Lidarr monitors this artist.
    /// </summary>
    public bool Monitored { get; init; }

    /// <summary>
    /// Tag ids on the artist.
    /// Resolve them to labels through Lidarr's /tag endpoint.
    /// </summary>
    public List<long> Tags { get; init; } = [];

    /// <summary>
    /// The quality profile assigned to the artist.
    /// </summary>
    public int QualityProfileId { get; init; }

    /// <summary>
    /// When the artist was added to Lidarr.
    /// </summary>
    public DateTimeOffset? Added { get; init; }

    /// <summary>
    /// Lidarr's status for the artist, such as continuing or ended.
    /// </summary>
    public string Status { get; init; } = string.Empty;
}
