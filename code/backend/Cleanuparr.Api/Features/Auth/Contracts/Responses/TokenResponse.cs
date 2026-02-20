namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record TokenResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
}
