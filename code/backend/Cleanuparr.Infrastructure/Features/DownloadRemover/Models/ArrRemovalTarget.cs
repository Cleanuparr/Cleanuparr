using System.Text.Json.Serialization;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

public sealed record ArrRemovalTarget : RemovalTarget
{
    public required QueueRecord Record { get; init; }

    public required SearchItem SearchItem { get; init; }

    /// <summary>
    /// The *arr removes the download from the client on our behalf.
    /// Mutually exclusive with <see cref="ChangeCategory"/>.
    /// </summary>
    public required bool RemoveFromClient { get; init; }

    /// <summary>
    /// The *arr moves the download to its post-import category instead of removing it.
    /// </summary>
    public bool ChangeCategory { get; init; }

    [JsonIgnore]
    public override string DownloadId => Record.DownloadId;

    [JsonIgnore]
    public override string Title => Record.Title;
}
