using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record CustomFormatScoreUpgradeResponse
{
    public Guid ArrInstanceId { get; init; }
    public long ExternalItemId { get; init; }
    public long EpisodeId { get; init; }
    public InstanceType ItemType { get; init; }
    public string Title { get; init; } = string.Empty;
    public int PreviousScore { get; init; }
    public int NewScore { get; init; }
    public int CutoffScore { get; init; }
    public DateTime UpgradedAt { get; init; }
}
