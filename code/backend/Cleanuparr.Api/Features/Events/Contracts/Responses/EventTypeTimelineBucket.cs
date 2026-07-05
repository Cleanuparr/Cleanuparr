namespace Cleanuparr.Api.Features.Events.Contracts.Responses;

public sealed record EventTypeTimelineBucket
{
    public DateOnly Date { get; init; }

    public Dictionary<string, int> Counts { get; init; } = new();
}
