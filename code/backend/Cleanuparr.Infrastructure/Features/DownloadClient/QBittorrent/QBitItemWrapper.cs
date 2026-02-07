using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent.Extensions;
using QBittorrent.Client;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;

/// <summary>
/// Wrapper for QBittorrent TorrentInfo that implements ITorrentItem interface
/// </summary>
public sealed class QBitItemWrapper : ITorrentItemWrapper
{
    private readonly IReadOnlyList<TorrentTracker> _trackers;

    public TorrentInfo Info { get; }

    public QBitItemWrapper(TorrentInfo torrentInfo, IReadOnlyList<TorrentTracker> trackers, bool isPrivate)
    {
        Info = torrentInfo ?? throw new ArgumentNullException(nameof(torrentInfo));
        _trackers = trackers ?? throw new ArgumentNullException(nameof(trackers));
        IsPrivate = isPrivate;
    }

    public string Hash => Info.Hash ?? string.Empty;
    
    public string Name => Info.Name ?? string.Empty;

    public bool IsPrivate { get; }

    public long Size => Info.Size;
    
    public double CompletionPercentage => Info.Progress * 100.0;
    
    public long DownloadedBytes => Info.Downloaded ?? 0;

    public long DownloadSpeed => Info.DownloadSpeed;
    
    public double Ratio => Info.Ratio;

    public long Eta => Info.EstimatedTime?.TotalSeconds is { } eta ? (long)eta : 0;
    
    public long SeedingTimeSeconds => Info.SeedingTime?.TotalSeconds is { } seedTime ? (long)seedTime : 0;

    public string? Category
    {
        get => Info.Category;
        set => Info.Category = value;
    }

    public string SavePath => throw new NotImplementedException();

    public IReadOnlyList<string> Tags => Info.Tags?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();

    public bool IsDownloading() => Info.State is TorrentState.Downloading or TorrentState.ForcedDownload;
    
    public bool IsStalled() => Info.State is TorrentState.StalledDownload;
    
    public bool IsSeeding() => Info.State is TorrentState.Uploading or TorrentState.ForcedUpload or TorrentState.StalledUpload;
    
    public bool IsMetadataDownloading() => Info.State is TorrentState.FetchingMetadata or TorrentState.ForcedFetchingMetadata;

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

            if (Info.Tags?.Contains(pattern, StringComparer.InvariantCultureIgnoreCase) is true)
            {
                return true;
            }

            if (_trackers.Any(tracker => tracker.ShouldIgnore(ignoredDownloads)))
            {
                return true;
            }
        }

        return false;
    }
}
