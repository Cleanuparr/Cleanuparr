namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed record OidcTokenExchangeResult
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
}
