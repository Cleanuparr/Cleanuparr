namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SearchableEpisode
{
    public long Id { get; init; }

    public bool HasFile { get; init; }

    public EpisodeFileInfo? EpisodeFile { get; init; }
}
