namespace Cleanuparr.Domain.Entities.Arr.Queue;

public sealed record QueueMovie
{
    public List<Image> Images { get; init; } = [];

    /// <summary>
    /// The ID of the movie file, zero when the movie has none.
    /// </summary>
    /// <remarks>
    /// Radarr and Whisparr v3 never fill <c>hasFile</c> in the queue, so read this field.
    /// </remarks>
    public long MovieFileId { get; init; }
}