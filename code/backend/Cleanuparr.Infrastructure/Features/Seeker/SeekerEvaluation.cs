namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// The units an instance needs searched, against the current cycle's history.
/// </summary>
internal sealed record SeekerEvaluation
{
    public required List<long> AllLibraryIds { get; init; }

    /// <summary>
    /// Radarr older than v5.6 has no wanted endpoints, so nothing got evaluated.
    /// </summary>
    public bool WantedUnsupported { get; init; }

    /// <summary>
    /// Every unit that needs a search, including the blocked and searched ones.
    /// </summary>
    public required List<SeekerSearchUnit> Units { get; init; }

    /// <summary>
    /// Latest search of each movie or series in the current cycle.
    /// </summary>
    public required Dictionary<long, DateTimeOffset> LastSearchedByItem { get; init; }

    /// <summary>
    /// Rebuilds the units as a fresh cycle sees them.
    /// </summary>
    public required Func<SeekerEvaluation> WithoutCycleHistory { get; init; }

    public List<SeekerSearchUnit> Remaining => Units.Where(u => u.BlockedBy is null && !u.Searched).ToList();
}
