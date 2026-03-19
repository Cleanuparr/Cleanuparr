namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SearchHistoryEntryResponse
{
    public Guid Id { get; init; }
    public Guid ArrInstanceId { get; init; }
    public string InstanceName { get; init; } = string.Empty;
    public string InstanceType { get; init; } = string.Empty;
    public long ExternalItemId { get; init; }
    public string ItemTitle { get; init; } = string.Empty;
    public int SeasonNumber { get; init; }
    public DateTime LastSearchedAt { get; init; }
    public int SearchCount { get; init; }
    public int TotalSearchCount { get; init; }
}
