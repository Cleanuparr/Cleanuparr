namespace Cleanuparr.Domain.Entities.Arr;

public sealed record ArrEpisodeFile
{
    public long Id { get; init; }

    public bool QualityCutoffNotMet { get; init; }

    public int CustomFormatScore { get; init; }
}
