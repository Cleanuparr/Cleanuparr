namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SeekerPreviewResponse
{
    public InstanceFunnelStat Funnel { get; init; } = new();

    /// <summary>
    /// Set when the cycle searched everything and waits out the minimum cycle time. Next up stays empty until then.
    /// </summary>
    public DateTimeOffset? CycleWaitingUntil { get; init; }

    /// <summary>
    /// Whether the selection strategy draws at random, so the next run can pick differently.
    /// </summary>
    public bool RandomPick { get; init; }

    public List<SeekerPreviewUnitResponse> NextUp { get; init; } = [];
    public List<SeekerPreviewExclusionResponse> Excluded { get; init; } = [];
}
