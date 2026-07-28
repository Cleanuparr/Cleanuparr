namespace Cleanuparr.Domain.Entities.Arr.Queue;

public sealed record ReadarrImage
{
    public string CoverType { get; init; } = string.Empty;

    public Uri? Url { get; init; }
}