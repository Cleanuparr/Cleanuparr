using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Entities.Deluge.Response;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

/// <summary>
/// Wrapper for Deluge DownloadStatus that implements ITorrentInfo interface
/// </summary>
public sealed class DelugeTorrentInfo : ITorrentInfo
{
    private readonly DownloadStatus _downloadStatus;

    public DelugeTorrentInfo(DownloadStatus downloadStatus)
    {
        _downloadStatus = downloadStatus ?? throw new ArgumentNullException(nameof(downloadStatus));
    }

    public string Hash => _downloadStatus.Hash ?? string.Empty;
    public string Name => _downloadStatus.Name ?? string.Empty;
    public bool IsPrivate => _downloadStatus.Private;
    public long Size => _downloadStatus.Size;
    
    public double CompletionPercentage => _downloadStatus.Size > 0 
        ? (_downloadStatus.TotalDone / (double)_downloadStatus.Size) * 100.0 
        : 0.0;
    public long DownloadedBytes => _downloadStatus.TotalDone;
    
    public IReadOnlyList<string> Trackers => _downloadStatus.Trackers?
        .Where(t => !string.IsNullOrEmpty(t.Url))
        .Select(t => ExtractHostFromUrl(t.Url!))
        .Where(host => !string.IsNullOrEmpty(host))
        .Distinct()
        .ToList()
        .AsReadOnly() ?? new List<string>().AsReadOnly();

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
