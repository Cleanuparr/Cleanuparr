using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record SearchEventResponse
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string InstanceName { get; init; } = string.Empty;
    public string? InstanceType { get; init; }
    public int ItemCount { get; init; }
    public List<string> Items { get; init; } = [];
    public SeekerSearchType SearchType { get; init; }
    public SearchCommandStatus? SearchStatus { get; init; }
    public DateTime? CompletedAt { get; init; }
    public object? GrabbedItems { get; init; }
    public Guid? CycleRunId { get; init; }
    public bool IsDryRun { get; init; }
}
