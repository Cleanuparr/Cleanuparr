namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SearchStatsSummaryResponse
{
    public int TotalSearchesAllTime { get; init; }
    public int SearchesLast7Days { get; init; }
    public int SearchesLast30Days { get; init; }
    public int UniqueItemsSearched { get; init; }
    public int PendingReplacementSearches { get; init; }
    public int EnabledInstances { get; init; }
    public List<InstanceSearchStat> PerInstanceStats { get; init; } = [];
}
