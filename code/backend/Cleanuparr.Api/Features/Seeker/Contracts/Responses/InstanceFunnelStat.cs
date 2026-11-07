using Cleanuparr.Persistence.Models.State;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

/// <summary>
/// How many items and units survived each Seeker stage on the cycle's latest run.
/// Items count movies or series, units count searches.
/// </summary>
public sealed record InstanceFunnelStat
{
    public int LibraryItems { get; init; }
    public int ReleasedItems { get; init; }
    public int MonitoredItems { get; init; }
    public int TagFilteredItems { get; init; }
    public int EligibleUnits { get; init; }
    public int QueuedUnits { get; init; }
    public int RecentlyGrabbedUnits { get; init; }
    public int RestingUnits { get; init; }
    public int SearchedUnits { get; init; }
    public int RemainingUnits { get; init; }
    public DateTimeOffset CountedAt { get; init; }

    /// <summary>
    /// Null until a run counted the cycle's funnel.
    /// </summary>
    public static InstanceFunnelStat? From(SeekerCycle? cycle) => cycle?.FunnelCountedAt is not { } countedAt
        ? null
        : new InstanceFunnelStat
        {
            LibraryItems = cycle.LibraryItems,
            ReleasedItems = cycle.ReleasedItems,
            MonitoredItems = cycle.MonitoredItems,
            TagFilteredItems = cycle.TagFilteredItems,
            EligibleUnits = cycle.EligibleNow,
            QueuedUnits = cycle.QueuedUnits,
            RecentlyGrabbedUnits = cycle.RecentlyGrabbedUnits,
            RestingUnits = cycle.RestingUnits,
            SearchedUnits = Math.Max(0, cycle.EligibleNow - cycle.QueuedUnits - cycle.RecentlyGrabbedUnits - cycle.RestingUnits - cycle.RemainingUnits),
            RemainingUnits = cycle.RemainingUnits,
            CountedAt = countedAt,
        };
}
