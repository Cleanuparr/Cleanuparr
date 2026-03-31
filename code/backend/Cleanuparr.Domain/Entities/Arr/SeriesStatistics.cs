namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SeriesStatistics
{
    public int EpisodeFileCount { get; init; }

    public int EpisodeCount { get; init; }
}