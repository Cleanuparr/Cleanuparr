using Cleanuparr.Persistence.Models.Configuration.General;

namespace Cleanuparr.Api.Features.General.Contracts.Responses;

public sealed record AuthConfigResponse
{
    public bool DisableAuthForLocalAddresses { get; init; }

    public bool TrustForwardedHeaders { get; init; }

    public required IReadOnlyList<string> TrustedNetworks { get; init; }

    public static AuthConfigResponse From(AuthConfig config) => new()
    {
        DisableAuthForLocalAddresses = config.DisableAuthForLocalAddresses,
        TrustForwardedHeaders = config.TrustForwardedHeaders,
        TrustedNetworks = config.TrustedNetworks,
    };
}
