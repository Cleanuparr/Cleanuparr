namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record CustomFormatScoreStatsResponse
{
    public int TotalTracked { get; init; }
    public int BelowCutoff { get; init; }
    public int AtOrAboveCutoff { get; init; }
    public int Monitored { get; init; }
    public int Unmonitored { get; init; }
    public int RecentUpgrades { get; init; }
    public List<InstanceCfScoreStat> PerInstanceStats { get; init; } = [];
}

public sealed record InstanceCfScoreStat
{
    public Guid InstanceId { get; init; }
    public string InstanceName { get; init; } = string.Empty;
    public string InstanceType { get; init; } = string.Empty;
    public int TotalTracked { get; init; }
    public int BelowCutoff { get; init; }
    public int AtOrAboveCutoff { get; init; }
    public int Monitored { get; init; }
    public int Unmonitored { get; init; }
    public int RecentUpgrades { get; init; }
}
