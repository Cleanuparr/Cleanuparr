namespace Cleanuparr.Domain.Entities;

/// <summary>
/// Universal abstraction for a torrent item across all download clients.
/// Provides a unified interface for accessing torrent properties and state.
/// </summary>
public interface ITorrentItemWrapper
{
    string Hash { get; }
    
    string Name { get; }

    bool IsPrivate { get; }

    long Size { get; }
    
    double CompletionPercentage { get; }
    
    long DownloadedBytes { get; }

    long DownloadSpeed { get; }
    
    double Ratio { get; }

    long Eta { get; }
    
    long SeedingTimeSeconds { get; }

    string? Category { get; set; }
    
    string SavePath { get; }

    bool IsDownloading();
    
    bool IsStalled();

    /// <summary>
    /// Determines if this torrent should be ignored based on the provided patterns.
    /// Checks if any pattern matches the torrent name, hash, or tracker.
    /// </summary>
    /// <param name="ignoredDownloads">List of patterns to check against</param>
    /// <returns>True if the torrent matches any ignore pattern</returns>
    bool IsIgnored(IReadOnlyList<string> ignoredDownloads);
}