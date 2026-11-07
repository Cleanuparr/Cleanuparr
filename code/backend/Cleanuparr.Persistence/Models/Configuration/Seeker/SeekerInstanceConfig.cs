using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Persistence.Models.Configuration.Seeker;

/// <summary>
/// Per-instance configuration for the Seeker job.
/// Links to an ArrInstance with cascade delete.
/// </summary>
public sealed record SeekerInstanceConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the arr instance this config belongs to
    /// </summary>
    public Guid ArrInstanceId { get; set; }

    /// <summary>
    /// Navigation property to the associated arr instance
    /// </summary>
    public ArrInstance ArrInstance { get; set; } = null!;

    /// <summary>
    /// Whether this instance is enabled for Seeker searches
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Arr tag IDs to exclude from search
    /// </summary>
    public List<string> SkipTags { get; set; } = [];

    /// <summary>
    /// Arr tag labels that limit search to items carrying at least one of them. Empty means no limit.
    /// Skip tags take precedence.
    /// </summary>
    public List<string> IncludeTags { get; set; } = [];

    /// <summary>
    /// Timestamp of when this instance was last processed (for round-robin scheduling)
    /// </summary>
    public DateTimeOffset? LastProcessedAt { get; set; }

    /// <summary>
    /// The current cycle ID. All searches in the same cycle share this ID.
    /// When all eligible items have been searched, a new ID is generated to start a fresh cycle.
    /// </summary>
    public Guid CurrentCycleId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Skip proactive search cycles when the number of actively downloading items
    /// (SizeLeft > 0) in the arr queue is at or above this threshold. 0 = disabled.
    /// The count includes only the items that a download client has.
    /// </summary>
    public int ActiveDownloadLimit { get; set; } = 3;

    /// <summary>
    /// Excludes downloads that carry a Queue Cleaner strike from the active download count.
    /// Counted strike types: stalled, slow speed, slow time, downloading metadata, errored.
    /// </summary>
    public bool IgnoreStruckDownloads { get; set; }

    /// <summary>
    /// Minimum number of days a cycle must span before a new one can start.
    /// If a cycle completes faster, no searches are triggered until this time has elapsed.
    /// </summary>
    public int MinCycleTimeDays { get; set; } = 7;

    /// <summary>
    /// Only search monitored items during proactive searches
    /// </summary>
    public bool MonitoredOnly { get; set; } = true;

    /// <summary>
    /// Skip items that already meet their quality cutoff during proactive searches
    /// </summary>
    public bool UseCutoff { get; set; }

    /// <summary>
    /// Search items whose custom format score is below the quality profile's cutoff format score
    /// </summary>
    public bool UseCustomFormatScore { get; set; }

    /// <summary>
    /// Skips items the arr grabbed within this many hours, even after the download left the queue. 0 = disabled.
    /// </summary>
    public int RecentGrabWindowHours { get; set; } = 24;

    /// <summary>
    /// How Sonarr seasons are searched. Ignored for Radarr.
    /// </summary>
    public SonarrSearchMode SonarrSearchMode { get; set; } = SonarrSearchMode.Auto;

    /// <summary>
    /// Validates this instance's configuration, throwing <see cref="CustomValidationException"/> on failure.
    /// </summary>
    public void Validate()
    {
        if (RecentGrabWindowHours is < 0 or > 168)
        {
            throw new CustomValidationException($"{nameof(RecentGrabWindowHours)} must be between 0 and 168");
        }
    }
}
