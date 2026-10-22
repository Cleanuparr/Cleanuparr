using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// One search: a movie, a whole Sonarr season, or a single episode when <see cref="EpisodeId"/> is set.
/// </summary>
public sealed record SeekerSearchUnit
{
    /// <summary>
    /// MovieId (Radarr) or SeriesId (Sonarr)
    /// </summary>
    public required long ItemId { get; init; }

    /// <summary>
    /// 0 for a movie.
    /// </summary>
    public required int SeasonNumber { get; init; }

    /// <summary>
    /// 0 for a movie or a season unit.
    /// </summary>
    public required long EpisodeId { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Null for a movie.
    /// </summary>
    public SeekerContentKind? ContentKind { get; init; }

    /// <summary>
    /// One reason per qualifying movie or episode the search covers.
    /// </summary>
    public required List<SeekerSearchReason> Reasons { get; init; }

    public DateTimeOffset? AirDateUtc { get; init; }

    /// <summary>
    /// When the movie or series joined the library.
    /// </summary>
    public DateTimeOffset? Added { get; init; }

    /// <summary>
    /// Latest search the arr recorded for the movie or any episode the unit covers.
    /// </summary>
    public DateTimeOffset? LastSearchTime { get; init; }

    /// <summary>
    /// Whether the current cycle already searched this unit.
    /// </summary>
    public required bool Searched { get; init; }

    /// <summary>
    /// <see cref="SeekerExclusionReason.InQueue"/> or <see cref="SeekerExclusionReason.RecentlyGrabbed"/> while a download covers the unit.
    /// </summary>
    public SeekerExclusionReason? BlockedBy { get; init; }
}
