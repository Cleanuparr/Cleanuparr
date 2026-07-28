namespace Cleanuparr.Domain.Entities.Arr.Queue;

/// <summary>
/// An image of a book in Readarr.
/// </summary>
public sealed record ReadarrImage
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
