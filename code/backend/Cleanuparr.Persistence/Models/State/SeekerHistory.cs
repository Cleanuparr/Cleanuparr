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
    /// When this item was last searched
    /// </summary>
    public DateTime LastSearchedAt { get; set; }
}
