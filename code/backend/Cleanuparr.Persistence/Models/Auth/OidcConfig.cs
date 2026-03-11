using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Shared.Attributes;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Auth;

[ComplexType]
public sealed record OidcConfig
{
    public bool Enabled { get; set; }

    /// <summary>
    /// The OIDC provider's issuer URL (e.g., https://authentik.example.com/application/o/cleanuparr/).
    /// Used to discover the .well-known/openid-configuration endpoints.
    /// </summary>
    [MaxLength(500)]
    public string IssuerUrl { get; set; } = string.Empty;

    /// <summary>
    /// The Client ID registered at the identity provider.
    /// </summary>
    [MaxLength(200)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The Client Secret (optional; for confidential clients).
    /// </summary>
    [SensitiveData]
    [MaxLength(500)]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Space-separated OIDC scopes to request.
    /// </summary>
    [MaxLength(500)]
    public string Scopes { get; set; } = "openid profile email";

    /// <summary>
    /// The OIDC subject ("sub" claim) that identifies the authorized user.
    /// Set during OIDC account linking. Only this subject can log in via OIDC.
    /// </summary>
    [MaxLength(500)]
    public string AuthorizedSubject { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the OIDC provider (shown on the login button, e.g., "Authentik").
    /// </summary>
    [MaxLength(100)]
    public string ProviderName { get; set; } = "OIDC";

    /// <summary>
    /// Optional full OIDC redirect URL (e.g., https://cleanuparr.example.com/api/auth/oidc/callback).
    /// When set, used directly as the callback URL instead of auto-detecting from the request.
    /// </summary>
    [MaxLength(500)]
    public string RedirectUrl { get; set; } = string.Empty;

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(IssuerUrl))
        {
            throw new ValidationException("OIDC Issuer URL is required when OIDC is enabled");
        }

        if (!Uri.TryCreate(IssuerUrl, UriKind.Absolute, out var issuerUri))
        {
            throw new ValidationException("OIDC Issuer URL must be a valid absolute URL");
        }

        // Enforce HTTPS except for localhost (development)
        if (issuerUri.Scheme != "https" && !IsLocalhost(issuerUri))
        {
            throw new ValidationException("OIDC Issuer URL must use HTTPS");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new ValidationException("OIDC Client ID is required when OIDC is enabled");
        }

        if (string.IsNullOrWhiteSpace(ProviderName))
        {
            throw new ValidationException("OIDC Provider Name is required when OIDC is enabled");
        }

        if (!string.IsNullOrWhiteSpace(RedirectUrl))
        {
            if (!Uri.TryCreate(RedirectUrl, UriKind.Absolute, out var redirectUri))
            {
                throw new ValidationException("OIDC Redirect URL must be a valid absolute URL");
            }

            if (redirectUri.Scheme is not ("http" or "https"))
            {
                throw new ValidationException("OIDC Redirect URL must use HTTP or HTTPS");
            }
        }
    }

    private static bool IsLocalhost(Uri uri)
    {
        return uri.Host is "localhost" or "127.0.0.1" or "::1" or "[::1]";
    }
}
