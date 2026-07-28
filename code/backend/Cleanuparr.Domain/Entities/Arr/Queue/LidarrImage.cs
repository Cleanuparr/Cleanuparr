namespace Cleanuparr.Domain.Entities.Arr.Queue;

public record LidarrImage
{
    public string CoverType { get; init; } = string.Empty;

    public Uri? Url { get; init; }
}