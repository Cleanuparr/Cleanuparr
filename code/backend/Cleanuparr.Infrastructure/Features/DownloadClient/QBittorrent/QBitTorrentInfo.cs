using Cleanuparr.Domain.Entities;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

/// <summary>
/// Wrapper for QBittorrent TorrentInfo that implements ITorrentInfo interface
/// </summary>
public sealed class QBitTorrentInfo : ITorrentInfo
{
    private readonly TorrentInfo _torrentInfo;
    private readonly IReadOnlyList<TorrentTracker> _trackers;
    private readonly bool _isPrivate;

    public QBitTorrentInfo(TorrentInfo torrentInfo, IReadOnlyList<TorrentTracker> trackers, bool isPrivate)
    {
        _torrentInfo = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
        _trackers = trackers ?? throw new ArgumentNullException(nameof(trackers));
        _isPrivate = isPrivate;
    }

    public string Hash => _torrentInfo.Hash ?? string.Empty;
    public string Name => _torrentInfo.Name ?? string.Empty;
    public bool IsPrivate => _isPrivate;
    public long Size => _torrentInfo.Size;
    
    public double CompletionPercentage => _torrentInfo.Progress * 100.0;
    public long DownloadedBytes => _torrentInfo.Downloaded ?? 0;
    
    public IReadOnlyList<string> Trackers => _trackers
        .Where(t => !string.IsNullOrEmpty(t.Url))
        .Select(t => ExtractHostFromUrl(t.Url!))
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly();

    /// <summary>
    /// Extracts the host from a tracker URL
    /// </summary>
    private static string ExtractHostFromUrl(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        
        return string.Empty;
    }
}