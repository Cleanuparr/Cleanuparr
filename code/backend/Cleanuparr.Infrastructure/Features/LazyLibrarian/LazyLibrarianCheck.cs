using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;

namespace Cleanuparr.Infrastructure.Features.LazyLibrarian;

/// <summary>
/// A download client's verdict on one LazyLibrarian item, normalised across the two jobs.
/// </summary>
public sealed record LazyLibrarianCheck
{
    public bool Found { get; init; }

    public bool ShouldRemove { get; init; }

    public bool RemoveFromClient { get; init; }

    public DeleteReason DeleteReason { get; init; }

    public ITorrentItemWrapper? Torrent { get; init; }
}
