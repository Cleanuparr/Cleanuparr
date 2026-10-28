namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableMovie
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool Monitored { get; init; }

    public bool HasFile { get; init; }

    public MovieFileInfo? MovieFile { get; init; }

    public List<long> Tags { get; init; } = [];

    public int QualityProfileId { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? Added { get; init; }

    public DateTimeOffset? DigitalRelease { get; init; }

    public DateTimeOffset? PhysicalRelease { get; init; }

    public DateTimeOffset? InCinemas { get; init; }

    /// <summary>
    /// Whisparr v3's only release date.
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; init; }

    /// <summary>
    /// Radarr's own verdict from the movie's minimum availability and the availability delay.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Set when a search reached an indexer; RSS grabs leave it alone.
    /// </summary>
    public DateTimeOffset? LastSearchTime { get; init; }
}
