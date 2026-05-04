using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

public sealed record QueueItemRemoveRequest<T>
    where T : SearchItem
{
    public required ArrInstance Instance { get; init; }

    public required T SearchItem { get; init; }

    public required QueueRecord Record { get; init; }

    public required bool RemoveFromClient { get; init; }

    public bool ChangeCategory { get; init; }

    public required DeleteReason DeleteReason { get; init; }

    public required Guid JobRunId { get; init; }

    public bool SkipSearch { get; init; }

    public DownloadClientConfig? DownloadClient { get; init; }
}
