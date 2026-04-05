using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// Represents a single item selected for proactive search.
/// </summary>
internal sealed record SeekerSearchCandidate
{
    /// <summary>
    /// MovieId (Radarr) or SeriesId (Sonarr)
    /// </summary>
    public required long ItemId { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Season number for Sonarr; 0 for Radarr.
    /// </summary>
    public required int SeasonNumber { get; init; }

    public required SeekerSearchReason Reason { get; init; }
}
