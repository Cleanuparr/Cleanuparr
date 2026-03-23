using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// Tracks the last time each media item was searched by the Seeker job.
/// Used by selection strategies to prioritize items that haven't been searched recently.
/// </summary>
public sealed record SeekerHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the arr instance this history belongs to
    /// </summary>
    public Guid ArrInstanceId { get; set; }

    /// <summary>
    /// Navigation property to the associated arr instance
    /// </summary>
    public ArrInstance ArrInstance { get; set; } = null!;

    /// <summary>
    /// The external item ID in the arr application (e.g., Radarr movieId or Sonarr seriesId)
    /// </summary>
    public long ExternalItemId { get; set; }

    /// <summary>
    /// The type of arr instance this item belongs to
    /// </summary>
    public InstanceType ItemType { get; set; }

    /// <summary>
    /// For Sonarr season-level searches, the season number that was searched
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// The run ID for cycle-based tracking. All searches in the same cycle share a RunId.
    /// When all items have been searched, a new RunId is generated to start a fresh cycle.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// When this item was last searched
    /// </summary>
    public DateTime LastSearchedAt { get; set; }

    /// <summary>
    /// Display name of the item (movie title, series name, etc.)
    /// </summary>
    public string ItemTitle { get; set; } = string.Empty;

    /// <summary>
    /// Running count of how many times this item has been searched
    /// </summary>
    public int SearchCount { get; set; } = 1;

    /// <summary>
    /// Whether this history entry was created during a dry run
    /// </summary>
    public bool IsDryRun { get; set; }
}
