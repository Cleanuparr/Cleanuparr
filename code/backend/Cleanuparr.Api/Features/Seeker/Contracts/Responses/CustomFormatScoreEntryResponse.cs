using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record CustomFormatScoreEntryResponse
{
    public Guid Id { get; init; }
    public Guid ArrInstanceId { get; init; }
    public long ExternalItemId { get; init; }
    public long EpisodeId { get; init; }
    public InstanceType ItemType { get; init; }
    public string Title { get; init; } = string.Empty;
    public long FileId { get; init; }
    public int CurrentScore { get; init; }
    public int CutoffScore { get; init; }
    public string QualityProfileName { get; init; } = string.Empty;
    public bool IsBelowCutoff { get; init; }
    public bool IsMonitored { get; init; }
    public DateTime LastSyncedAt { get; init; }
    
    /// <summary>
    /// Timestamp at which this item last saw its custom format score strictly
    /// exceed the prior recorded score. Null when no upgrade has been recorded.
    /// </summary>
    public DateTime? LastUpgradedAt { get; init; }
}
