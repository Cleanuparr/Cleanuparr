using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;

/// <summary>
/// Aggregates every capability interface so a single NSubstitute mock can stand in for any torrent client
/// across the job tests, regardless of which capability the job under test actually narrows to.
/// </summary>
public interface IMockDownloadService :
    IQueueCheckCapable, IFileBlockingCapable, IOrphanClaimsCapable, ISeedingCleanupCapable, IUnlinkedCapable,
    IDeadTorrentCapable
{
}
