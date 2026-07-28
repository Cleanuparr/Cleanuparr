namespace Cleanuparr.Domain.Entities.Arr.Queue;

/// <summary>
/// An image of an album in Lidarr.
/// </summary>
public record LidarrImage
{
    /// <summary>
    /// The type of the image, for example "cover".
    /// </summary>
    public string CoverType { get; init; } = string.Empty;

    /// <summary>
    /// The address of the image.
    /// </summary>
    public Uri? Url { get; init; }
}
