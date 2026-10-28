namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableEpisode
{
    public long Id { get; init; }

    public long SeriesId { get; init; }

    public int SeasonNumber { get; init; }

    public int EpisodeNumber { get; init; }

    public bool Monitored { get; init; }

    public DateTimeOffset? AirDateUtc { get; init; }

    public bool HasFile { get; init; }

    public long EpisodeFileId { get; init; }

    /// <summary>
    /// Set when a search reached an indexer; RSS grabs leave it alone.
    /// </summary>
    public DateTimeOffset? LastSearchTime { get; init; }
}
