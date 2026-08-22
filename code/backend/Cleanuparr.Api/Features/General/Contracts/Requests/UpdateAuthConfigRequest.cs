using Cleanuparr.Persistence.Models.Configuration.General;

namespace Cleanuparr.Api.Features.General.Contracts.Requests;

public sealed record UpdateAuthConfigRequest
{
    public bool DisableAuthForLocalAddresses { get; init; }

    public bool TrustForwardedHeaders { get; init; }

    public List<string> TrustedNetworks { get; init; } = [];

    public void ApplyTo(AuthConfig existingConfig)
    {
        existingConfig.DisableAuthForLocalAddresses = DisableAuthForLocalAddresses;
        existingConfig.TrustForwardedHeaders = TrustForwardedHeaders;
        existingConfig.TrustedNetworks = TrustedNetworks;
    }
}
