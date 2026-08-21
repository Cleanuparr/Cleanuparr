namespace Cleanuparr.Api.Features.Status.Contracts.Responses;

public sealed record ApplicationStatusResponse
{
    public required string Version { get; init; }

    public required DateTime StartTime { get; init; }

    public required TimeSpan UpTime { get; init; }

    public required double MemoryUsageMB { get; init; }

    public required TimeSpan ProcessorTime { get; init; }
}
