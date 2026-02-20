namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record AuthStatusResponse
{
    public required bool SetupCompleted { get; init; }
    public bool PlexLinked { get; init; }
}
