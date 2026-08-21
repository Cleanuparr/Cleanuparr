using System.Text.Json.Serialization;
using Cleanuparr.Domain.Entities.LazyLibrarian;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

public sealed record LazyLibrarianRemovalTarget : RemovalTarget
{
    public required LazyLibrarianQueueItem Item { get; init; }

    /// <summary>
    /// Whether Cleanuparr already removed the torrent from the client.
    /// LazyLibrarian only clears the snatch once the client no longer holds it.
    /// </summary>
    public required bool RemovedFromClient { get; init; }

    [JsonIgnore]
    public override string DownloadId => Item.DownloadId;

    [JsonIgnore]
    public override string Title => Item.Title;
}
