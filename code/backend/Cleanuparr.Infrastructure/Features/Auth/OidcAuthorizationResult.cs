namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed record OidcAuthorizationResult
{
    public required string AuthorizationUrl { get; init; }
    public required string State { get; init; }
}
