using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.DownloadClient;

public sealed record DownloadCheckResult
{
    /// <summary>
    /// True if the download should be removed; otherwise false.
    /// </summary>
    public bool ShouldRemove { get; set; }

    public DeleteReason DeleteReason { get; set; }

    /// <summary>
    /// True if the download is private; otherwise false.
    /// </summary>
    public bool IsPrivate { get; set; }

    public bool Found { get; set; }

    /// <summary>
    /// True if the download should be deleted from the client; otherwise false.
    /// </summary>
    public bool DeleteFromClient { get; set; }

    /// <summary>
    /// True if the matching queue rule asked to change the category in the *arr instead of deleting; otherwise false.
    /// </summary>
    public bool ChangeCategory { get; set; }

    /// <summary>
    /// The matched torrent located in the download client during evaluation. Populated when
    /// <see cref="Found"/> is true so that callers (e.g. LazyLibrarian, which lacks an
    /// arr-driven removeFromClient equivalent) can act on it without a second lookup.
    /// </summary>
    public ITorrentItemWrapper? Torrent { get; set; }
}