using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// Historical record of custom format score changes.
/// Only written when a score value actually changes (deduplication).
/// </summary>
public sealed record CustomFormatScoreHistory
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
    /// The custom format score at the time of recording
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// The cutoff format score from the quality profile at the time of recording
    /// </summary>
    public int CutoffScore { get; set; }

    /// <summary>
    /// When this score was recorded
    /// </summary>
    public DateTime RecordedAt { get; set; }
}
