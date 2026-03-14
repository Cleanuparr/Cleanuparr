namespace Cleanuparr.Domain.Entities.Arr;

public sealed record EpisodeFileInfo
{
    public bool QualityCutoffNotMet { get; init; }
}