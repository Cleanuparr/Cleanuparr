namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record CustomFormatScoreStatsResponse
{
    public int TotalTracked { get; init; }
    public int BelowCutoff { get; init; }
    public int AtOrAboveCutoff { get; init; }
    public int RecentUpgrades { get; init; }
    public double AverageScore { get; init; }
}
