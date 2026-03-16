using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// Represents a pending reactive search request queued after a download removal.
/// The Seeker processes these items with priority before proactive searches.
/// </summary>
public sealed record SearchQueueItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the arr instance this search targets
    /// </summary>
    public Guid ArrInstanceId { get; set; }

    /// <summary>
    /// Navigation property to the associated arr instance
    /// </summary>
    /// 
    public ArrInstance ArrInstance { get; set; } = null!;

    /// <summary>
    /// The item ID to search for (movieId, episodeId, albumId, etc.)
    /// </summary>
    public long ItemId { get; set; }

    /// <summary>
    /// For Sonarr/Whisparr: the series ID when searching at episode/season level
    /// </summary>
    public long? SeriesId { get; set; }

    /// <summary>
    /// For Sonarr/Whisparr: the search type ("Episode" or "Season")
    /// </summary>
    public string? SearchType { get; set; }

    /// <summary>
    /// Display title for logging and event publishing
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// When this search request was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
