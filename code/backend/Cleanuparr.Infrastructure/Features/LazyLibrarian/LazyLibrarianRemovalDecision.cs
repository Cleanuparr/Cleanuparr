using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.LazyLibrarian;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

/// <summary>
/// One item a job decided to remove, with everything the handler needs to act.
/// </summary>
public sealed record LazyLibrarianRemovalDecision
{
    public required LazyLibrarianQueueItem Item { get; init; }

    public required DeleteReason DeleteReason { get; init; }

    /// <summary>
    /// The intent. LazyLibrarian refuses to remove a task it adopted, so the handler can still decline.
    /// </summary>
    public required bool RemoveFromClient { get; init; }

    public DownloadClientConfig? DownloadClient { get; init; }

    public IDownloadService? DownloadService { get; init; }

    public ITorrentItemWrapper? Torrent { get; init; }
}
