namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SeasonStatistics
{
    public int EpisodeCount { get; init; }

    public int EpisodeFileCount { get; init; }

    public int TotalEpisodeCount { get; init; }

    public DateTimeOffset? NextAiring { get; init; }
}
