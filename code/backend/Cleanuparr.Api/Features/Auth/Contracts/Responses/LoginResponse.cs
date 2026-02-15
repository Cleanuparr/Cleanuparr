namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record LoginResponse
{
    public required bool RequiresTwoFactor { get; init; }
    public string? LoginToken { get; init; }
}
