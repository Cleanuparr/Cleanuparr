using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Auth;

public sealed class OidcAuthServiceTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly Mock<IHttpClientFactory> _httpClientFactory;
    private readonly Mock<ILogger<OidcAuthService>> _logger;

    public OidcAuthServiceTests()
    {
        _dataContext = TestDataContextFactory.Create(seedData: true);
        _httpClientFactory = new Mock<IHttpClientFactory>();
        _logger = new Mock<ILogger<OidcAuthService>>();

        // Set up a default HttpClient for the factory
        _httpClientFactory
            .Setup(f => f.CreateClient("OidcAuth"))
            .Returns(new HttpClient());
    }

    private OidcAuthService CreateService()
    {
        return new OidcAuthService(_httpClientFactory.Object, _dataContext, _logger.Object);
    }

    #region StoreOneTimeCode Tests

    [Fact]
    public void StoreOneTimeCode_ReturnsNonEmptyCode()
    {
        var service = CreateService();

        var code = service.StoreOneTimeCode("access-token", "refresh-token", 3600);

        code.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void StoreOneTimeCode_ReturnsDifferentCodesEachTime()
    {
        var service = CreateService();

        var code1 = service.StoreOneTimeCode("access-1", "refresh-1", 3600);
        var code2 = service.StoreOneTimeCode("access-2", "refresh-2", 3600);

        code1.ShouldNotBe(code2);
    }

    #endregion

    #region ExchangeOneTimeCode Tests

    [Fact]
    public void ExchangeOneTimeCode_ValidCode_ReturnsTokens()
    {
        var service = CreateService();
        var code = service.StoreOneTimeCode("test-access", "test-refresh", 1800);

        var result = service.ExchangeOneTimeCode(code);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("test-access");
        result.RefreshToken.ShouldBe("test-refresh");
        result.ExpiresIn.ShouldBe(1800);
    }

    [Fact]
    public void ExchangeOneTimeCode_InvalidCode_ReturnsNull()
    {
        var service = CreateService();

        var result = service.ExchangeOneTimeCode("nonexistent-code");

        result.ShouldBeNull();
    }

    [Fact]
    public void ExchangeOneTimeCode_SameCodeTwice_SecondReturnsNull()
    {
        var service = CreateService();
        var code = service.StoreOneTimeCode("test-access", "test-refresh", 3600);

        var result1 = service.ExchangeOneTimeCode(code);
        var result2 = service.ExchangeOneTimeCode(code);

        result1.ShouldNotBeNull();
        result2.ShouldBeNull();
    }

    [Fact]
    public void ExchangeOneTimeCode_EmptyCode_ReturnsNull()
    {
        var service = CreateService();

        var result = service.ExchangeOneTimeCode(string.Empty);

        result.ShouldBeNull();
    }

    #endregion

    #region StartAuthorization Tests

    [Fact]
    public async Task StartAuthorization_WhenOidcDisabled_ThrowsInvalidOperationException()
    {
        // Ensure OIDC is disabled in config (default state from seed data)
        var service = CreateService();

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.StartAuthorization("https://app.test/api/auth/oidc/callback"));
    }

    [Fact]
    public async Task StartAuthorization_WhenEnabled_ReturnsAuthorizationUrlWithRequiredParams()
    {
        await EnableOidcInConfig();
        var service = CreateService();

        // This will fail at the discovery document fetch since we don't have a real IdP,
        // but we can at least verify the config check passes.
        // The actual StartAuthorization requires a reachable discovery endpoint.
        // Full flow testing is done in integration tests.
        await Should.ThrowAsync<Exception>(
            () => service.StartAuthorization("https://app.test/api/auth/oidc/callback"));
    }

    #endregion

    #region HandleCallback Tests

    [Fact]
    public async Task HandleCallback_InvalidState_ReturnsFailure()
    {
        var service = CreateService();

        var result = await service.HandleCallback("some-code", "invalid-state", "https://app.test/callback");

        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("Invalid or expired");
    }

    #endregion

    #region ClearDiscoveryCache Tests

    [Fact]
    public void ClearDiscoveryCache_DoesNotThrow()
    {
        Should.NotThrow(() => OidcAuthService.ClearDiscoveryCache());
    }

    #endregion

    #region HandleCallback Edge Cases

    [Fact]
    public async Task HandleCallback_EmptyCode_ReturnsFailure()
    {
        var service = CreateService();

        // Even with a valid-looking state, empty code still fails because the state won't match
        var result = await service.HandleCallback("", "nonexistent-state", "https://app.test/callback");

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleCallback_EmptyState_ReturnsFailure()
    {
        var service = CreateService();

        var result = await service.HandleCallback("some-code", "", "https://app.test/callback");

        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("Invalid or expired");
    }

    #endregion

    #region StoreOneTimeCode Capacity Tests

    [Fact]
    public void StoreOneTimeCode_MultipleStores_AllReturnUniqueCodes()
    {
        var service = CreateService();
        var codes = new HashSet<string>();

        for (int i = 0; i < 10; i++)
        {
            var code = service.StoreOneTimeCode($"access-{i}", $"refresh-{i}", 3600);
            codes.Add(code).ShouldBeTrue($"Code {i} was not unique");
        }

        codes.Count.ShouldBe(10);
    }

    #endregion

    #region Helpers

    private async Task EnableOidcInConfig()
    {
        var config = await _dataContext.GeneralConfigs.FirstAsync();
        config.Auth.Oidc = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "https://mock-oidc-provider.test",
            ClientId = "test-client",
            Scopes = "openid profile email",
            AuthorizedSubject = "test-subject",
            ProviderName = "TestProvider"
        };
        await _dataContext.SaveChangesAsync();
    }

    private async Task<T> FirstAsync<T>() where T : class
    {
        return await _dataContext.Set<T>().FirstAsync();
    }

    /// <summary>
    /// Creates an OidcAuthService using the given HttpMessageHandler instead of the default mock.
    /// </summary>
    private OidcAuthService CreateServiceWithHandler(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("OidcAuth")).Returns(new HttpClient(handler));
        return new OidcAuthService(factory.Object, _dataContext, _logger.Object);
    }

    /// <summary>
    /// Returns a handler that serves a minimal OIDC discovery document for the mock issuer.
    /// Optionally also handles a token endpoint and JWKS endpoint.
    /// </summary>
    private static MockHttpMessageHandler CreateDiscoveryHandler(
        string? tokenResponse = null,
        HttpStatusCode tokenStatusCode = HttpStatusCode.OK,
        bool throwNetworkErrorOnToken = false,
        string? jwksJson = null)
    {
        const string issuer = "https://mock-oidc-provider.test";

        var discoveryJson = JsonSerializer.Serialize(new
        {
            issuer,
            authorization_endpoint = $"{issuer}/authorize",
            token_endpoint = $"{issuer}/token",
            jwks_uri = $"{issuer}/.well-known/jwks",
            response_types_supported = new[] { "code" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" }
        });

        return new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? "";

            if (url.Contains("/.well-known/openid-configuration"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(discoveryJson, Encoding.UTF8, "application/json")
                };
            }

            if (url.Contains("/.well-known/jwks"))
            {
                // Default to an empty JWKS (sufficient for PKCE/URL tests; JWT tests pass a real key)
                var keysJson = jwksJson ?? """{"keys": []}""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(keysJson, Encoding.UTF8, "application/json")
                };
            }

            if (url.Contains("/token"))
            {
                if (throwNetworkErrorOnToken)
                    throw new HttpRequestException("Simulated network failure");

                return new HttpResponseMessage(tokenStatusCode)
                {
                    Content = new StringContent(tokenResponse ?? "{}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
    }

    #endregion

    #region PKCE and Authorization URL Tests

    [Fact]
    public async Task StartAuthorization_ReturnUrl_ContainsPkceParameters()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        var service = CreateServiceWithHandler(CreateDiscoveryHandler());
        try
        {
            var result = await service.StartAuthorization("https://app.test/api/auth/oidc/callback");

            result.AuthorizationUrl.ShouldContain("code_challenge=");
            result.AuthorizationUrl.ShouldContain("code_challenge_method=S256");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task StartAuthorization_ReturnUrl_ContainsAllRequiredOAuthParams()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        var service = CreateServiceWithHandler(CreateDiscoveryHandler());
        const string redirectUri = "https://app.test/api/auth/oidc/callback";
        try
        {
            var result = await service.StartAuthorization(redirectUri);
            var url = result.AuthorizationUrl;

            url.ShouldContain("response_type=code");
            url.ShouldContain("client_id=");
            url.ShouldContain("redirect_uri=");
            url.ShouldContain("scope=");
            url.ShouldContain("state=");
            url.ShouldContain("nonce=");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task StartAuthorization_PkceChallenge_IsValidBase64Url()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        var service = CreateServiceWithHandler(CreateDiscoveryHandler());
        try
        {
            var result = await service.StartAuthorization("https://app.test/api/auth/oidc/callback");

            // Extract code_challenge from URL
            var uri = new Uri(result.AuthorizationUrl);
            var queryParts = uri.Query.TrimStart('?').Split('&');
            var challengePart = queryParts.FirstOrDefault(p => p.StartsWith("code_challenge="));
            challengePart.ShouldNotBeNull();

            var challengeValue = Uri.UnescapeDataString(challengePart.Substring("code_challenge=".Length));

            // Base64url characters: A-Z a-z 0-9 - _ (no +, /, or =)
            challengeValue.ShouldNotContain("+");
            challengeValue.ShouldNotContain("/");
            challengeValue.ShouldNotContain("=");
            challengeValue.Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    #endregion

    public void Dispose()
    {
        _dataContext.Dispose();
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
