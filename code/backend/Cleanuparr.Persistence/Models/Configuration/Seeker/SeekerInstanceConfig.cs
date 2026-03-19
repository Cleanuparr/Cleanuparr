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
    /// The current cycle run ID. All searches in the same cycle share this ID.
    /// When all eligible items have been searched, a new ID is generated to start a fresh cycle.
    /// </summary>
    public Guid CurrentRunId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Total number of eligible items in the library for this instance.
    /// Updated each time the Seeker processes the instance.
    /// </summary>
    public int TotalEligibleItems { get; set; }
}
