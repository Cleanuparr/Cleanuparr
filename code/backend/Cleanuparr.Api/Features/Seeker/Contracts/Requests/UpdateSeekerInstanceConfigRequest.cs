namespace Cleanuparr.Api.Features.Seeker.Contracts.Requests;

public sealed record UpdateSeekerInstanceConfigRequest
{
    public Guid ArrInstanceId { get; init; }

    public bool Enabled { get; init; } = true;

    public List<string> SkipTags { get; init; } = [];

    public int ActiveDownloadLimit { get; init; } = 3;

    public int MinCycleTimeDays { get; init; } = 7;
}