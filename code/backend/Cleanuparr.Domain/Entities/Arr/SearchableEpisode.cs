namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableEpisode
{
    public long Id { get; init; }

    public int SeasonNumber { get; init; }

    public int EpisodeNumber { get; init; }

    public bool Monitored { get; init; }

    public DateTime? AirDateUtc { get; init; }

    public bool HasFile { get; init; }

    public long EpisodeFileId { get; init; }

    public EpisodeFileInfo? EpisodeFile { get; init; }
}
