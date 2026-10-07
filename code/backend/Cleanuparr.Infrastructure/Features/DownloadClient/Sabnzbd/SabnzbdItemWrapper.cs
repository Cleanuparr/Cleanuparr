using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Sabnzbd;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

/// <summary>
/// Wraps a SABnzbd queue slot (in progress) or history slot (finished) as an <see cref="IDownloadItem"/>.
/// SABnzbd has no seeding/tracker concepts, so it carries no torrent-only members.
/// </summary>
public sealed class SabnzbdItemWrapper : IDownloadItem
{
    private readonly SabnzbdQueueSlot? _queueSlot;
    private readonly SabnzbdHistorySlot? _historySlot;

    /// <summary>
    /// True when this item was found in SABnzbd's history (finished, successfully or not) rather than the active queue.
    /// </summary>
    public bool IsInHistory => _historySlot is not null;

    public SabnzbdItemWrapper(SabnzbdQueueSlot queueSlot)
    {
        _queueSlot = queueSlot ?? throw new ArgumentNullException(nameof(queueSlot));
        Category = queueSlot.Category;
    }

    public SabnzbdItemWrapper(SabnzbdHistorySlot historySlot)
    {
        _historySlot = historySlot ?? throw new ArgumentNullException(nameof(historySlot));
        Category = historySlot.Category;
    }

    public string DownloadId => _queueSlot?.NzoId ?? _historySlot?.NzoId ?? string.Empty;

    public string Name => _queueSlot?.Filename ?? _historySlot?.Name ?? string.Empty;

    public long Size => _historySlot is not null
        ? _historySlot.Bytes
        : (long)((_queueSlot?.Mb ?? 0) * 1024 * 1024);

    public double CompletionPercentage
    {
        get
        {
            if (_historySlot is not null)
            {
                return 100.0;
            }

            double mb = _queueSlot?.Mb ?? 0;
            double mbLeft = _queueSlot?.MbLeft ?? 0;
            return mb > 0 ? (mb - mbLeft) / mb * 100.0 : 0.0;
        }
    }

    public long DownloadedBytes => _historySlot is not null
        ? _historySlot.Bytes
        : (long)(((_queueSlot?.Mb ?? 0) - (_queueSlot?.MbLeft ?? 0)) * 1024 * 1024);

    public long DownloadSpeed => 0;

    public string? Category { get; set; }

    public string SavePath => _historySlot?.Storage ?? string.Empty;

    /// <summary>
    /// The raw SABnzbd status string, e.g. "Downloading", "Paused", "Completed", "Failed".
    /// </summary>
    public string? Status => _queueSlot?.Status ?? _historySlot?.Status;

    public bool IsStopped => false;

    public bool IsDownloading() => _queueSlot?.Status is "Downloading" or "Extracting" or "Verifying" or "Repairing" or "Moving" or "QuickCheck" or "Checking" or "Fetching";

    public bool IsIgnored(IReadOnlyList<string> ignoredDownloads)
    {
        if (ignoredDownloads.Count == 0)
        {
            return false;
        }

        foreach (string pattern in ignoredDownloads)
        {
            if (DownloadId.Equals(pattern, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (Category?.Equals(pattern, StringComparison.InvariantCultureIgnoreCase) is true)
            {
                return true;
            }
        }

        return false;
    }
}
