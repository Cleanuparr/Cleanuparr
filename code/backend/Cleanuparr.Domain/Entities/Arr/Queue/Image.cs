namespace Cleanuparr.Domain.Entities.Arr.Queue;

/// <summary>
/// An image of a series or a movie in Sonarr, Radarr or Whisparr.
/// </summary>
public record Image
{
    /// <summary>
    /// The type of the image, for example "poster" or "screenshot".
    /// </summary>
    public string CoverType { get; init; } = string.Empty;

    /// <summary>
    /// The address of the image on the server of the metadata provider.
    /// </summary>
    public Uri? RemoteUrl { get; init; }
}
