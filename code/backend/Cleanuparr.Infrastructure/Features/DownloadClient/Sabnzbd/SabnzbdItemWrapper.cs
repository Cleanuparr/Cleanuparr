using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Sabnzbd;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Sabnzbd;

/// <summary>
/// Wraps a SABnzbd queue slot (in progress) or history slot (finished) as an <see cref="ITorrentItemWrapper"/>.
/// SABnzbd reports download speed for the server as a whole, not per job, and has no seeding/tracker concepts.
/// </summary>
public sealed class SabnzbdItemWrapper : ITorrentItemWrapper
{
    private readonly SabnzbdQueueSlot? _queueSlot;
    private readonly SabnzbdHistorySlot? _historySlot;
    private readonly double _serverKbPerSec;
    private readonly bool _queuePaused;

    /// <summary>
    /// True when this item was found in SABnzbd's history (finished, successfully or not) rather than the active queue.
    /// </summary>
    public bool IsInHistory => _historySlot is not null;

    public SabnzbdItemWrapper(SabnzbdQueueSlot queueSlot, double serverKbPerSec, bool queuePaused = false)
    {
        _queueSlot = queueSlot ?? throw new ArgumentNullException(nameof(queueSlot));
        _serverKbPerSec = serverKbPerSec;
        _queuePaused = queuePaused;
        Category = queueSlot.Category;
    }

    public SabnzbdItemWrapper(SabnzbdHistorySlot historySlot)
    {
        _historySlot = historySlot ?? throw new ArgumentNullException(nameof(historySlot));
        Category = historySlot.Category;
    }

    public string Hash => _queueSlot?.NzoId ?? _historySlot?.NzoId ?? string.Empty;

    public string Name => _queueSlot?.Filename ?? _historySlot?.Name ?? string.Empty;

    public bool IsPrivate => false;

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

    /// <remarks>SABnzbd reports one download speed for the whole server; this is only meaningful while this job is the one actively downloading.</remarks>
    public long DownloadSpeed => _queueSlot?.Status == "Downloading" ? (long)(_serverKbPerSec * 1024) : 0;

    public double Ratio => 0;

    public int? SeederCount => null;

    public TrackerHealth TrackerHealth => TrackerHealth.Unsupported;

    public DateTimeOffset? AddedOn => null;

    public long Eta => 0;

    public long SeedingTimeSeconds => 0;

    public DateTime? LastActivityTime => null;

    public string? Category { get; set; }

    public string SavePath => _historySlot?.Storage ?? string.Empty;

    public IReadOnlyList<string> TrackerDomains => [];

    public IReadOnlyList<string> Tags => [];

    /// <remarks>A global pause leaves each slot's own status unchanged (e.g. still "Queued"), so a job already in progress counts as stopped too; one still at 0% is just waiting its turn.</remarks>
    public bool IsStopped => _queueSlot?.Status == "Paused" || (_queuePaused && HasPartialProgress);

    public bool IsDownloading() => _queueSlot?.Status is "Downloading" or "Extracting" or "Verifying" or "Repairing" or "Moving" or "QuickCheck" or "Checking" or "Fetching";

    /// <remarks>SABnzbd fails a job that stops progressing on its own and moves it to history as "Failed"; the queue cleaner acts on that instead of guessing from a live queue slot.</remarks>
    public bool IsStalled() => false;

    private bool HasPartialProgress => _queueSlot is { Mb: > 0 } slot && slot.MbLeft < slot.Mb;

    public bool IsIgnored(IReadOnlyList<string> ignoredDownloads)
    {
        if (ignoredDownloads.Count == 0)
        {
            return false;
        }

        foreach (string pattern in ignoredDownloads)
        {
            if (Hash.Equals(pattern, StringComparison.InvariantCultureIgnoreCase))
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
