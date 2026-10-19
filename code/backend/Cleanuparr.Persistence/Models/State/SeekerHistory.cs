using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Persistence.Models.State;

/// <summary>
/// One Seeker search unit: a movie, a season, or an episode.
/// Tracks the last search and the lifetime results of searching it.
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
    /// For Sonarr episode searches, the episode that was searched. 0 for a season or movie unit.
    /// </summary>
    public long EpisodeId { get; set; }

    /// <summary>
    /// What the unit holds
    /// </summary>
    public SeekerContentKind ContentKind { get; set; }

    /// <summary>
    /// The last cycle this unit was searched in
    /// </summary>
    public Guid CycleId { get; set; }

    /// <summary>
    /// When this item was last searched
    /// </summary>
    public DateTimeOffset LastSearchedAt { get; set; }

    /// <summary>
    /// Display name of the item (movie title, series name, etc.)
    /// </summary>
    public string ItemTitle { get; set; } = string.Empty;

    /// <summary>
    /// Lifetime count of searches for this unit
    /// </summary>
    public int SearchCount { get; set; } = 1;

    /// <summary>
    /// Lifetime count of searches for this unit that grabbed a release
    /// </summary>
    public int Grabs { get; set; }

    /// <summary>
    /// Result of the last search. Null until that search finishes.
    /// </summary>
    public SeekerSearchOutcome? LastOutcome { get; set; }

    /// <summary>
    /// Whether this history entry was created during a dry run
    /// </summary>
    public bool IsDryRun { get; set; }
}
