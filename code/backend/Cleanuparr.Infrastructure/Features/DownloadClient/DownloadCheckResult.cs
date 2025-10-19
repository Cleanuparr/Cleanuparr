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
}