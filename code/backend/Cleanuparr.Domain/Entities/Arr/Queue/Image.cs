namespace Cleanuparr.Domain.Entities.Arr.Queue;

public record Image
{
    public string CoverType { get; init; } = string.Empty;

    public Uri? RemoteUrl { get; init; }
}