using Cleanuparr.Domain.Entities;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

/// <summary>
/// Wrapper for Transmission TorrentInfo that implements ITorrentInfo interface
/// </summary>
public sealed class TransmissionTorrentInfo : ITorrentInfo
{
    private readonly TorrentInfo _torrentInfo;

    public TransmissionTorrentInfo(TorrentInfo torrentInfo)
    {
        _torrentInfo = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
    }

    public string Hash => _torrentInfo.HashString ?? string.Empty;
    public string Name => _torrentInfo.Name ?? string.Empty;
    public bool IsPrivate => _torrentInfo.IsPrivate ?? false;
    public long Size => _torrentInfo.TotalSize ?? 0;
    
    public double CompletionPercentage => _torrentInfo.TotalSize > 0 
        ? ((_torrentInfo.DownloadedEver ?? 0) / (double)_torrentInfo.TotalSize) * 100.0 
        : 0.0;
    
    public IReadOnlyList<string> Trackers => _torrentInfo.Trackers?
        .Where(t => !string.IsNullOrEmpty(t.Announce))
        .Select(t => ExtractHostFromUrl(t.Announce!))
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

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