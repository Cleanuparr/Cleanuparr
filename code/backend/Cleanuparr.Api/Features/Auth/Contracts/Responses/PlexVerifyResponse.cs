namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record PlexVerifyResponse
{
    public required bool Completed { get; init; }
    public TokenResponse? Tokens { get; init; }
}
