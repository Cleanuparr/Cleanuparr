namespace Cleanuparr.Domain.Entities.Arr;

public sealed record EpisodeFileInfo
{
    public long Id { get; init; }

    public bool QualityCutoffNotMet { get; init; }
}