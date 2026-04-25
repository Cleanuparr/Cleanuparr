using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// Current custom format score state for a library item.
/// Updated periodically by the CustomFormatScoreSyncer job.
/// </summary>
public sealed record CustomFormatScoreEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the arr instance
    /// </summary>
    public Guid ArrInstanceId { get; set; }

    /// <summary>
    /// Navigation property to the associated arr instance
    /// </summary>
    public ArrInstance ArrInstance { get; set; } = null!;

    /// <summary>
    /// The external item ID (Radarr movieId or Sonarr seriesId)
    /// </summary>
    public long ExternalItemId { get; set; }

    /// <summary>
    /// For Sonarr episodes, the episode ID. 0 for Radarr movies.
    /// </summary>
    public long EpisodeId { get; set; }

    /// <summary>
    /// The type of arr instance (Radarr/Sonarr)
    /// </summary>
    public InstanceType ItemType { get; set; }

    /// <summary>
    /// The item title for display purposes
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The movie file ID or episode file ID in the arr app
    /// </summary>
    public long FileId { get; set; }

    /// <summary>
    /// The current custom format score of the file
    /// </summary>
    public int CurrentScore { get; set; }

    /// <summary>
    /// The cutoff format score from the quality profile
    /// </summary>
    public int CutoffScore { get; set; }

    /// <summary>
    /// The quality profile name for display purposes
    /// </summary>
    public string QualityProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the item is currently monitored in the arr app
    /// </summary>
    public bool IsMonitored { get; set; }

    /// <summary>
    /// When this entry was last synced from the arr API
    /// </summary>
    public DateTime LastSyncedAt { get; set; }

    /// <summary>
    /// When this item last saw a score upgrade (current score strictly exceeded the prior recorded score).
    /// Null when the item has no recorded upgrades.
    /// </summary>
    public DateTime? LastUpgradedAt { get; set; }
}
