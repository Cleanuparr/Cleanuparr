using Cleanuparr.Persistence.Models.Auth;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Api.Features.Auth.Contracts.Responses;

public sealed record OidcConfigResponse
{
    public bool Enabled { get; init; }

    public required string IssuerUrl { get; init; }

    public required string ClientId { get; init; }

    [SensitiveData]
    public required string ClientSecret { get; init; }

    public required string Scopes { get; init; }

    public required string AuthorizedSubject { get; init; }

    public required string ProviderName { get; init; }

    public required string RedirectUrl { get; init; }

    public bool ExclusiveMode { get; init; }

    public static OidcConfigResponse From(OidcConfig config) => new()
    {
        Enabled = config.Enabled,
        IssuerUrl = config.IssuerUrl,
        ClientId = config.ClientId,
        ClientSecret = config.ClientSecret,
        Scopes = config.Scopes,
        AuthorizedSubject = config.AuthorizedSubject,
        ProviderName = config.ProviderName,
        RedirectUrl = config.RedirectUrl,
        ExclusiveMode = config.ExclusiveMode,
    };
}
