using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

public sealed record QueueItemRemoveRequest
{
    public required ArrInstance Instance { get; init; }

    public required RemovalTarget Target { get; init; }

    public required DeleteReason DeleteReason { get; init; }

    public required Guid JobRunId { get; init; }

    public bool SkipSearch { get; init; }

    public DownloadClientConfig? DownloadClient { get; init; }
}
