namespace Cleanuparr.Api.Features.Events.Contracts.Responses;

public sealed record EventTypeTimelineBucket
{
    public DateTimeOffset Date { get; init; }

    public Dictionary<string, int> Counts { get; init; } = new();
}
