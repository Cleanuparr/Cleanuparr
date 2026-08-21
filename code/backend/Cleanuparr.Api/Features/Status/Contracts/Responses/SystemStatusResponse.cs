namespace Cleanuparr.Api.Features.Status.Contracts.Responses;

public sealed record SystemStatusResponse
{
    public required ApplicationStatusResponse Application { get; init; }

    public required Dictionary<string, MediaManagerStatusResponse> MediaManagers { get; init; }
}
