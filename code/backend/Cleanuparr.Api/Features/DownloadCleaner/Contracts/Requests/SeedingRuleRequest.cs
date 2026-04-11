using System.ComponentModel.DataAnnotations;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

public record SeedingRuleRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Categories this rule applies to. At least one must be specified.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one category must be specified.")]
    public List<string> Categories { get; init; } = [];

    /// <summary>
    /// Tracker domain suffixes to match (e.g. "tracker.example.com"). Empty = any tracker.
    /// </summary>
    public List<string> TrackerPatterns { get; init; } = [];

    /// <summary>
    /// Torrent must have at least one of these tags/labels. Accepted for all clients;
    /// silently ignored for Deluge, rTorrent, and µTorrent.
    /// </summary>
    public List<string> TagsAny { get; init; } = [];

    /// <summary>
    /// Torrent must have ALL of these tags/labels. Accepted for all clients;
    /// silently ignored for Deluge, rTorrent, and µTorrent.
    /// </summary>
    public List<string> TagsAll { get; init; } = [];

    /// <summary>
    /// Evaluation priority (lower = evaluated first). Auto-assigned if not provided.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Priority must be a positive integer.")]
    public int? Priority { get; init; }

    /// <summary>
    /// Which torrent privacy types this rule applies to.
    /// </summary>
    public TorrentPrivacyType PrivacyType { get; init; } = TorrentPrivacyType.Public;

    /// <summary>
    /// Max ratio before removing a download.
    /// </summary>
    public double MaxRatio { get; init; } = -1;

    /// <summary>
    /// Min number of hours to seed before removing a download, if the ratio has been met.
    /// </summary>
    public double MinSeedTime { get; init; }

    /// <summary>
    /// Number of hours to seed before removing a download.
    /// </summary>
    public double MaxSeedTime { get; init; } = -1;

    /// <summary>
    /// Whether to delete the source files when cleaning the download.
    /// </summary>
    public bool DeleteSourceFiles { get; init; } = true;
}
