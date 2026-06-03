using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

public sealed record BlockFilesResult
{
    /// <summary>
    /// True if the download should be removed; otherwise false.
    /// </summary>
    public bool ShouldRemove { get; set; }

    /// <summary>
    /// True if the download is private; otherwise false.
    /// </summary>
    public bool IsPrivate { get; set; }

    public bool Found { get; set; }

    public DeleteReason DeleteReason { get; set; } = DeleteReason.None;

    /// <summary>
    /// The matched torrent located in the download client during evaluation. Populated when
    /// <see cref="Found"/> is true so that callers (e.g. LazyLibrarian) can act on it
    /// without a second lookup.
    /// </summary>
    public ITorrentItemWrapper? Torrent { get; set; }
}