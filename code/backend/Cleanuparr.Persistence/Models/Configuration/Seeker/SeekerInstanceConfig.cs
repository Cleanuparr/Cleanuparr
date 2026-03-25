using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    /// Timestamp of when this instance was last processed (for round-robin scheduling)
    /// </summary>
    public DateTime? LastProcessedAt { get; set; }

    /// <summary>
    /// The current cycle ID. All searches in the same cycle share this ID.
    /// When all eligible items have been searched, a new ID is generated to start a fresh cycle.
    /// </summary>
    public Guid CurrentCycleId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Total number of eligible items in the library for this instance.
    /// Updated each time the Seeker processes the instance.
    /// </summary>
    public int TotalEligibleItems { get; set; }

    /// <summary>
    /// Skip proactive search cycles when the number of actively downloading items
    /// (SizeLeft > 0) in the arr queue is at or above this threshold. 0 = disabled.
    /// </summary>
    public int ActiveDownloadLimit { get; set; } = 3;

    /// <summary>
    /// Minimum number of days a cycle must span before a new one can start.
    /// If a cycle completes faster, no searches are triggered until this time has elapsed.
    /// </summary>
    public int MinCycleTimeDays { get; set; } = 7;
}
