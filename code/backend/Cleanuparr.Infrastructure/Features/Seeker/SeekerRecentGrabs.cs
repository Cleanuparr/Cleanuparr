namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// What the arr grabbed within the instance's recent grab window.
/// </summary>
public sealed record SeekerRecentGrabs
{
    public HashSet<long> MovieIds { get; init; } = [];

    public HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)> Episodes { get; init; } = [];
}
