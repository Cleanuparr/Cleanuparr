namespace Cleanuparr.Domain.Entities.Arr;

public sealed record MovieFileInfo
{
    public long Id { get; init; }

    public bool QualityCutoffNotMet { get; init; }
}