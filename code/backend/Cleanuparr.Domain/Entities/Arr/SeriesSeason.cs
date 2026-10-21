namespace Cleanuparr.Domain.Entities.Arr;

public sealed record SeriesSeason
{
    public int SeasonNumber { get; init; }

    public bool Monitored { get; init; }

    public SeasonStatistics? Statistics { get; init; }
}
