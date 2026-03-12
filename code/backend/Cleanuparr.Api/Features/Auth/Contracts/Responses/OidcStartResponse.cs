namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record OidcStartResponse
{
    public required string AuthorizationUrl { get; init; }
}
