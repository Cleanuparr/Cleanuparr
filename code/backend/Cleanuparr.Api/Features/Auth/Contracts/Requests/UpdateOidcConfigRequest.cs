using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence.Models.Auth;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Api.Features.Auth.Contracts.Requests;

public sealed record UpdateOidcConfigRequest
{
    public bool Enabled { get; init; }

    public string IssuerUrl { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string Scopes { get; init; } = "openid profile email";

    public string ProviderName { get; init; } = "OIDC";

    public string RedirectUrl { get; init; } = string.Empty;

    public bool ExclusiveMode { get; init; }

    public void ApplyTo(OidcConfig existingConfig)
    {
        var previousIssuerUrl = existingConfig.IssuerUrl;

        existingConfig.Enabled = Enabled;
        existingConfig.IssuerUrl = IssuerUrl;
        existingConfig.ClientId = ClientId;
        existingConfig.Scopes = Scopes;
        existingConfig.ProviderName = ProviderName;
        existingConfig.RedirectUrl = RedirectUrl;
        existingConfig.ExclusiveMode = ExclusiveMode;

        if (!ClientSecret.IsPlaceholder())
        {
            existingConfig.ClientSecret = ClientSecret;
        }
        
        // AuthorizedSubject is intentionally NOT mapped here — it is set only via the OIDC link callback

        if (previousIssuerUrl != IssuerUrl)
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }
}
