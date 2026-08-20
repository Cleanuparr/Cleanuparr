namespace Cleanuparr.Api.Features.Status.Contracts.Responses;

public sealed record InstanceConnectionResponse
{
    public required string Name { get; init; }

    public required Uri Url { get; init; }

    public required bool IsConnected { get; init; }

    public required string Message { get; init; }
}
