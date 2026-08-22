using Cleanuparr.Shared.Helpers;
using Microsoft.IdentityModel.Protocols;

namespace Cleanuparr.Infrastructure.Features.Auth;

/// <summary>
/// Fetches the OIDC discovery document with a fresh client on every call.
/// <see cref="OidcAuthService"/> caches its configuration managers in a static field.
/// Holding one client there pins its handler past the factory's rotation window.
/// </summary>
internal sealed class FactoryDocumentRetriever(
    IHttpClientFactory httpClientFactory,
    bool requireHttps) : IDocumentRetriever
{
    public Task<string> GetDocumentAsync(string address, CancellationToken cancel) =>
        new HttpDocumentRetriever(httpClientFactory.CreateClient(Constants.HttpClientOidcAuthName))
            { RequireHttps = requireHttps }
            .GetDocumentAsync(address, cancel);
}
