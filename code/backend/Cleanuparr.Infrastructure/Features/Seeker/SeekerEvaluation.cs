using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// The units an instance needs searched, against the current cycle's history.
/// </summary>
public sealed record SeekerEvaluation
{
    public required List<long> AllLibraryIds { get; init; }

    /// <summary>
    /// Radarr older than v5.6 has no wanted endpoints, so nothing got evaluated.
    /// </summary>
    public bool WantedUnsupported { get; init; }

    /// <summary>
    /// Movies or series past the release and grace period stage.
    /// </summary>
    public int ReleasedItems { get; init; }

    /// <summary>
    /// Released items past the monitored stage.
    /// </summary>
    public int MonitoredItems { get; init; }

    /// <summary>
    /// Monitored items past the tag filters.
    /// </summary>
    public int TagFilteredItems { get; init; }

    /// <summary>
    /// Items and episodes dropped before they became units.
    /// </summary>
    public List<SeekerExclusion> ItemExclusions { get; init; } = [];

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

    public int QueuedUnits => Units.Count(u => u.BlockedBy is SeekerExclusionReason.InQueue);

    public int RecentlyGrabbedUnits => Units.Count(u => u.BlockedBy is SeekerExclusionReason.RecentlyGrabbed);

    /// <summary>
    /// Everything that did not reach <see cref="Remaining"/>, with one reason each.
    /// </summary>
    public IEnumerable<SeekerExclusion> Excluded => ItemExclusions.Concat(Units
        .Where(u => u.BlockedBy is not null || u.Searched)
        .Select(u => new SeekerExclusion(u.Name, u.BlockedBy ?? SeekerExclusionReason.SearchedThisCycle)));
}
