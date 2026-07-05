namespace Cleanuparr.Api.Features.Events.Contracts.Responses;

public sealed record EventTypeTimelineResponse
{
    public List<string> Types { get; init; } = [];

    public List<EventTypeTimelineBucket> Buckets { get; init; } = [];
}
