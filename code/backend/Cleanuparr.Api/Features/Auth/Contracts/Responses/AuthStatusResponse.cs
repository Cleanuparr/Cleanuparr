namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record AuthStatusResponse
{
    public required bool SetupCompleted { get; init; }
    public bool PlexLinked { get; init; }
    public bool AuthBypassActive { get; init; }
    public bool OidcEnabled { get; init; }
    public string OidcProviderName { get; init; } = string.Empty;
}
