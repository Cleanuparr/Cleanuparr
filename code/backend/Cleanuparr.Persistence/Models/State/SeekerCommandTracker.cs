using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// Tracks arr command IDs returned from search requests so the SeekerCommandMonitor
/// can poll for completion status and inspect the download queue afterward.
/// </summary>
public sealed record SeekerCommandTracker
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the arr instance this command was sent to
    /// </summary>
    public Guid ArrInstanceId { get; set; }

    /// <summary>
    /// Navigation property to the associated arr instance
    /// </summary>
    public ArrInstance ArrInstance { get; set; } = null!;

    /// <summary>
    /// The command ID returned by the arr API
    /// </summary>
    public long CommandId { get; set; }

    /// <summary>
    /// The AppEvent ID to update when the command completes
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// The external item ID that was searched (movieId or seriesId)
    /// </summary>
    public long ExternalItemId { get; set; }

    /// <summary>
    /// Display name of the item that was searched
    /// </summary>
    public string ItemTitle { get; set; } = string.Empty;

    /// <summary>
    /// The type of arr instance (Radarr, Sonarr, etc.)
    /// </summary>
    public InstanceType ItemType { get; set; }

    /// <summary>
    /// For Sonarr season-level searches, the season number that was searched.
    /// 0 for Radarr or when not applicable.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// When this tracker entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current status of the arr command
    /// </summary>
    public SearchCommandStatus Status { get; set; } = SearchCommandStatus.Pending;
}
