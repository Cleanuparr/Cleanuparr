namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record InstanceSearchStat
{
    public Guid InstanceId { get; init; }
    public string InstanceName { get; init; } = string.Empty;
    public string InstanceType { get; init; } = string.Empty;
    public int ItemsTracked { get; init; }
    public int TotalSearchCount { get; init; }
    public DateTime? LastSearchedAt { get; init; }
    public DateTime? LastProcessedAt { get; init; }
    public Guid? CurrentCycleId { get; init; }
    public int CycleItemsSearched { get; init; }
    public int CycleItemsTotal { get; init; }
    public DateTime? CycleStartedAt { get; init; }
}
