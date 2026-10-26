namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableAlbum
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public long ArtistId { get; init; }

    public bool Monitored { get; init; }

    public DateTimeOffset? ReleaseDate { get; init; }

    /// <summary>
    /// Set when a search reached an indexer; RSS grabs leave it alone.
    /// </summary>
    public DateTimeOffset? LastSearchTime { get; init; }

    public AlbumStatistics? Statistics { get; init; }
}
