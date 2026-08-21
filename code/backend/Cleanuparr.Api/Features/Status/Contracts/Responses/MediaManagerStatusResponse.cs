namespace Cleanuparr.Api.Features.Status.Contracts.Responses;

public sealed record MediaManagerStatusResponse
{
    public required int InstanceCount { get; init; }
}
