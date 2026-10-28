using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// One Sonarr search: a whole season, or a single episode when <see cref="EpisodeId"/> is set.
/// </summary>
internal sealed record SonarrSearchUnit
{
    public required long SeriesId { get; init; }

    public required int SeasonNumber { get; init; }

    /// <summary>
    /// 0 for a season unit.
    /// </summary>
    public required long EpisodeId { get; init; }

    public required string Name { get; init; }

    public required SeekerContentKind ContentKind { get; init; }

    /// <summary>
    /// One reason per qualifying episode the search covers.
    /// </summary>
    public required List<SeekerSearchReason> Reasons { get; init; }

    public DateTimeOffset? AirDateUtc { get; init; }

    /// <summary>
    /// Whether the current cycle already searched this unit.
    /// </summary>
    public required bool Searched { get; init; }
}
