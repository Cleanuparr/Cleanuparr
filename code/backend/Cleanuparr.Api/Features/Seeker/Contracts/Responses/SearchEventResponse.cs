using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SearchEventResponse
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public Guid? ArrInstanceId { get; init; }
    public string? InstanceType { get; init; }
    public string ItemTitle { get; init; } = string.Empty;
    public SeekerSearchType SearchType { get; init; }
    public SeekerSearchReason? SearchReason { get; init; }
    public SearchCommandStatus? SearchStatus { get; init; }
    public DateTime? CompletedAt { get; init; }
    public List<string> GrabbedItems { get; init; } = [];
    public Guid? CycleId { get; init; }
    public bool IsDryRun { get; init; }
}
