namespace Cleanuparr.Domain.Entities;

/// <summary>
/// Universal abstraction for a torrent item across all download clients.
/// Provides a unified interface for accessing torrent properties and state.
/// </summary>
public interface ITorrentItemWrapper
{
    // Basic identification
    string Hash { get; }
    string Name { get; }

    // Privacy and tracking
    bool IsPrivate { get; }

    // Size and progress
    long Size { get; }
    double CompletionPercentage { get; }
    long DownloadedBytes { get; }

    // Speed and transfer rates
    long DownloadSpeed { get; }
    double Ratio { get; }

    // Time tracking
    long Eta { get; }
    long SeedingTimeSeconds { get; }

    string? Category { get; }

    // State checking methods
    bool IsDownloading();
    bool IsStalled();
    bool IsSeeding();
    bool IsCompleted();
    bool IsPaused();
    bool IsQueued();
    bool IsChecking();

    // Filtering methods
    /// <summary>
    /// Determines if this torrent should be ignored based on the provided patterns.
    /// Checks if any pattern matches the torrent name, hash, or tracker.
    /// </summary>
    /// <param name="ignoredDownloads">List of patterns to check against</param>
    /// <returns>True if the torrent matches any ignore pattern</returns>
    bool IsIgnored(IReadOnlyList<string> ignoredDownloads);
}