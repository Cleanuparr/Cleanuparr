using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.UTorrent.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;

/// <summary>
/// Wrapper for UTorrent UTorrentItem and UTorrentProperties that implements ITorrentInfo interface
/// </summary>
public sealed class UTorrentTorrentInfo : ITorrentInfo
{
    private readonly UTorrentItem _torrentItem;
    private readonly UTorrentProperties _torrentProperties;

    public UTorrentTorrentInfo(UTorrentItem torrentItem, UTorrentProperties torrentProperties)
    {
        _torrentItem = torrentItem ?? throw new ArgumentNullException(nameof(torrentItem));
        _torrentProperties = torrentProperties ?? throw new ArgumentNullException(nameof(torrentProperties));
    }

    public string Hash => _torrentItem.Hash;
    public string Name => _torrentItem.Name;
    public bool IsPrivate => _torrentProperties.IsPrivate;
    public long Size => _torrentItem.Size;
    
    public double CompletionPercentage => _torrentItem.Progress / 10.0; // Progress is in permille (1000 = 100%)
    public long DownloadedBytes => _torrentItem.Downloaded;
    
    public IReadOnlyList<string> Trackers => _torrentProperties.TrackerList
        .Select(ExtractHostFromUrl)
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