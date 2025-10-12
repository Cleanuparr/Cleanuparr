namespace Cleanuparr.Domain.Entities;

/// <summary>
/// Universal abstraction for a torrent item across all download clients.
/// Provides a unified interface for accessing torrent properties and state.
/// </summary>
public interface ITorrentItem
{
    // Basic identification
    string Hash { get; }
    string Name { get; }

    // Privacy and tracking
    bool IsPrivate { get; }
    IReadOnlyList<string> Trackers { get; }

    // Size and progress
    long Size { get; }
    double CompletionPercentage { get; }
    long DownloadedBytes { get; }
    long TotalUploaded { get; }

    // Speed and transfer rates
    long DownloadSpeed { get; }
    long UploadSpeed { get; }
    double Ratio { get; }

    // Time tracking
    long Eta { get; }
    DateTime? DateAdded { get; }
    DateTime? DateCompleted { get; }
    long SeedingTimeSeconds { get; }

    // Categories and tags
    string? Category { get; }
    IReadOnlyList<string> Tags { get; }

    // State checking methods
    bool IsDownloading();
    bool IsStalled();
    bool IsSeeding();
    bool IsCompleted();
    bool IsPaused();
    bool IsQueued();
    bool IsChecking();
    bool IsAllocating();
    bool IsMetadataDownloading();

    // Filtering methods
    /// <summary>
    /// Determines if this torrent should be ignored based on the provided patterns.
    /// Checks if any pattern matches the torrent name, hash, or tracker.
    /// </summary>
    /// <param name="ignoredDownloads">List of patterns to check against</param>
    /// <returns>True if the torrent matches any ignore pattern</returns>
    bool IsIgnored(IReadOnlyList<string> ignoredDownloads);
}