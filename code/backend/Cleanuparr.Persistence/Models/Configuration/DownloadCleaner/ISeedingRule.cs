using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public interface ISeedingRule : IConfig
{
    Guid Id { get; set; }

    Guid DownloadClientConfigId { get; set; }

    DownloadClientConfig DownloadClientConfig { get; set; }

    /// <summary>
    /// Human-readable display label for this rule.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// The torrent categories/labels this rule applies to. At least one must be specified.
    /// </summary>
    List<string> Categories { get; set; }

    /// <summary>
    /// Tracker domain patterns to filter by (suffix match, case-insensitive).
    /// Empty list means match any tracker.
    /// </summary>
    List<string> TrackerPatterns { get; set; }

    /// <summary>
    /// Evaluation order. Lower value = evaluated first. Auto-assigned on create.
    /// </summary>
    int Priority { get; set; }

    TorrentPrivacyType PrivacyType { get; set; }

    double MaxRatio { get; set; }

    double MinSeedTime { get; set; }

    double MaxSeedTime { get; set; }

    bool DeleteSourceFiles { get; set; }
}
