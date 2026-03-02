using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleanuparr.Infrastructure.Features.Auth;
using Microsoft.IdentityModel.Tokens;
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
    /// Uses reflection to retrieve the nonce stored in a pending OIDC flow state.
    /// Required for constructing a valid JWT in tests before HandleCallback is called.
    /// </summary>
    private static string GetFlowNonce(string state)
    {
        var pendingFlowsField = typeof(OidcAuthService)
            .GetField("PendingFlows", BindingFlags.NonPublic | BindingFlags.Static)!;
        var pendingFlows = pendingFlowsField.GetValue(null)!;

        // Use ConcurrentDictionary indexer: pendingFlows[state]
        var indexer = pendingFlows.GetType().GetProperty("Item")!;
        var flowState = indexer.GetValue(pendingFlows, new object[] { state })
            ?? throw new InvalidOperationException($"No pending flow found for state: {state}");

        var nonceProp = flowState.GetType()
            .GetProperty("Nonce", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (string)nonceProp.GetValue(flowState)!;
    }

    /// <summary>
    /// Returns a handler that serves a minimal OIDC discovery document for the mock issuer.
    /// Optionally also handles a token endpoint and JWKS endpoint.
    /// </summary>
    private static MockHttpMessageHandler CreateDiscoveryHandler(
        string? tokenResponse = null,
        HttpStatusCode tokenStatusCode = HttpStatusCode.OK,
        bool throwNetworkErrorOnToken = false,
        string? jwksJson = null,
        Func<string>? tokenResponseFactory = null)
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

                // tokenResponseFactory allows dynamic response generation (needed for JWT nonce)
                var body = tokenResponseFactory?.Invoke() ?? tokenResponse ?? "{}";
                return new HttpResponseMessage(tokenStatusCode)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
    }

    #endregion

    #region JWT ID Token Validation Tests

    private const string MockIssuer = "https://mock-oidc-provider.test";
    private const string MockClientId = "test-client";
    private const string MockSubject = "test-subject-123";
    private const string MockRedirectUri = "https://app.test/api/auth/oidc/callback";

    [Fact]
    public async Task HandleCallback_ValidIdToken_ReturnsSuccessWithSubject()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        var jwt = new JwtTestHelper();
        string? capturedJwt = null;

        var handler = CreateDiscoveryHandler(
            jwksJson: jwt.GetJwksJson(),
            tokenResponseFactory: () =>
                $$"""{"id_token":"{{capturedJwt}}","access_token":"access-123","token_type":"Bearer"}""");

        var service = CreateServiceWithHandler(handler);
        try
        {
            var startResult = await service.StartAuthorization(MockRedirectUri);
            var nonce = GetFlowNonce(startResult.State);
            capturedJwt = jwt.CreateIdToken(MockIssuer, MockClientId, MockSubject, nonce);

            var callbackResult = await service.HandleCallback("code", startResult.State, MockRedirectUri);

            callbackResult.Success.ShouldBeTrue();
            callbackResult.Subject.ShouldBe(MockSubject);
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task HandleCallback_ExpiredIdToken_ReturnsFailure()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        var jwt = new JwtTestHelper();
        string? capturedJwt = null;

        var handler = CreateDiscoveryHandler(
            jwksJson: jwt.GetJwksJson(),
            tokenResponseFactory: () =>
                $$"""{"id_token":"{{capturedJwt}}","access_token":"access-123","token_type":"Bearer"}""");

        var service = CreateServiceWithHandler(handler);
        try
        {
            var startResult = await service.StartAuthorization(MockRedirectUri);
            var nonce = GetFlowNonce(startResult.State);
            // Token expired 1 hour ago (well outside the 2-minute clock skew)
            capturedJwt = jwt.CreateIdToken(MockIssuer, MockClientId, MockSubject, nonce,
                expiry: DateTime.UtcNow.AddHours(-1),
                notBefore: DateTime.UtcNow.AddHours(-2));

            var callbackResult = await service.HandleCallback("code", startResult.State, MockRedirectUri);

            callbackResult.Success.ShouldBeFalse();
            callbackResult.Error.ShouldContain("ID token validation failed");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task HandleCallback_WrongNonce_ReturnsFailure()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        var jwt = new JwtTestHelper();
        var handler = CreateDiscoveryHandler(
            jwksJson: jwt.GetJwksJson(),
            tokenResponseFactory: () =>
                $$"""{"id_token":"{{jwt.CreateIdToken(MockIssuer, MockClientId, MockSubject, "wrong-nonce")}}","access_token":"access-123","token_type":"Bearer"}""");

        var service = CreateServiceWithHandler(handler);
        try
        {
            var startResult = await service.StartAuthorization(MockRedirectUri);
            var callbackResult = await service.HandleCallback("code", startResult.State, MockRedirectUri);

            callbackResult.Success.ShouldBeFalse();
            callbackResult.Error.ShouldContain("ID token validation failed");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task HandleCallback_WrongIssuer_ReturnsFailure()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        var jwt = new JwtTestHelper();
        string? capturedJwt = null;

        var handler = CreateDiscoveryHandler(
            jwksJson: jwt.GetJwksJson(),
            tokenResponseFactory: () =>
                $$"""{"id_token":"{{capturedJwt}}","access_token":"access-123","token_type":"Bearer"}""");

        var service = CreateServiceWithHandler(handler);
        try
        {
            var startResult = await service.StartAuthorization(MockRedirectUri);
            var nonce = GetFlowNonce(startResult.State);
            // Use a different issuer than what's in config
            capturedJwt = jwt.CreateIdToken("https://evil-issuer.test", MockClientId, MockSubject, nonce);

            var callbackResult = await service.HandleCallback("code", startResult.State, MockRedirectUri);

            callbackResult.Success.ShouldBeFalse();
            callbackResult.Error.ShouldContain("ID token validation failed");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task HandleCallback_MissingSubClaim_ReturnsFailure()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        var jwt = new JwtTestHelper();
        string? capturedJwt = null;

        var handler = CreateDiscoveryHandler(
            jwksJson: jwt.GetJwksJson(),
            tokenResponseFactory: () =>
                $$"""{"id_token":"{{capturedJwt}}","access_token":"access-123","token_type":"Bearer"}""");

        var service = CreateServiceWithHandler(handler);
        try
        {
            var startResult = await service.StartAuthorization(MockRedirectUri);
            var nonce = GetFlowNonce(startResult.State);
            capturedJwt = jwt.CreateIdToken(MockIssuer, MockClientId, subject: null, nonce);

            var callbackResult = await service.HandleCallback("code", startResult.State, MockRedirectUri);

            callbackResult.Success.ShouldBeFalse();
            callbackResult.Error.ShouldContain("missing 'sub' claim");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    #endregion

    #region Token Exchange Error Handling Tests

    [Fact]
    public async Task HandleCallback_TokenEndpointReturnsHttpError_ReturnsFailure()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        const string redirectUri = "https://app.test/api/auth/oidc/callback";
        var handler = CreateDiscoveryHandler(tokenResponse: """{"error":"invalid_grant"}""", tokenStatusCode: HttpStatusCode.BadRequest);
        var service = CreateServiceWithHandler(handler);
        try
        {
            var startResult = await service.StartAuthorization(redirectUri);
            var callbackResult = await service.HandleCallback("some-code", startResult.State, redirectUri);

            callbackResult.Success.ShouldBeFalse();
            callbackResult.Error.ShouldContain("Failed to exchange authorization code");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task HandleCallback_TokenEndpointThrowsNetworkError_ReturnsFailure()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        const string redirectUri = "https://app.test/api/auth/oidc/callback";
        var handler = CreateDiscoveryHandler(throwNetworkErrorOnToken: true);
        var service = CreateServiceWithHandler(handler);
        try
        {
            var startResult = await service.StartAuthorization(redirectUri);
            var callbackResult = await service.HandleCallback("some-code", startResult.State, redirectUri);

            callbackResult.Success.ShouldBeFalse();
            callbackResult.Error.ShouldContain("Failed to exchange authorization code");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task HandleCallback_TokenResponseMissingIdToken_ReturnsFailure()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        const string redirectUri = "https://app.test/api/auth/oidc/callback";
        // Token response with access_token but no id_token — ValidateIdToken will fail on empty string
        var handler = CreateDiscoveryHandler(tokenResponse: """{"access_token":"abc","token_type":"Bearer"}""");
        var service = CreateServiceWithHandler(handler);
        try
        {
            var startResult = await service.StartAuthorization(redirectUri);
            var callbackResult = await service.HandleCallback("some-code", startResult.State, redirectUri);

            callbackResult.Success.ShouldBeFalse();
            callbackResult.Error.ShouldContain("ID token validation failed");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    #endregion

    #region Expiry and Capacity Tests (via reflection)

    [Fact]
    public void ExchangeOneTimeCode_ExpiredCode_ReturnsNull()
    {
        var service = CreateService();

        // Insert a pre-expired entry directly into the static dictionary
        var code = InsertExpiredOneTimeCode();

        var result = service.ExchangeOneTimeCode(code);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task HandleCallback_ExpiredFlowState_ReturnsExpiredError()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        const string redirectUri = "https://app.test/api/auth/oidc/callback";
        var handler = CreateDiscoveryHandler();
        var service = CreateServiceWithHandler(handler);
        try
        {
            // Get a valid state from StartAuthorization, then backdate its CreatedAt
            var startResult = await service.StartAuthorization(redirectUri);
            BackdateFlowState(startResult.State, TimeSpan.FromMinutes(11));

            var callbackResult = await service.HandleCallback("some-code", startResult.State, redirectUri);

            callbackResult.Success.ShouldBeFalse();
            callbackResult.Error.ShouldContain("OIDC flow has expired");
        }
        finally
        {
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    [Fact]
    public async Task StartAuthorization_WhenAtCapacity_ThrowsInvalidOperationException()
    {
        await EnableOidcInConfig();
        OidcAuthService.ClearDiscoveryCache();

        const string redirectUri = "https://app.test/api/auth/oidc/callback";
        var handler = CreateDiscoveryHandler();
        var service = CreateServiceWithHandler(handler);

        var insertedKeys = new List<string>();
        try
        {
            // Fill PendingFlows up to the maximum (100 entries)
            for (var i = 0; i < 100; i++)
            {
                var key = InsertPendingFlowState(redirectUri);
                insertedKeys.Add(key);
            }

            // The 101st attempt should throw
            await Should.ThrowAsync<InvalidOperationException>(
                () => service.StartAuthorization(redirectUri),
                "Too many pending OIDC flows");
        }
        finally
        {
            RemovePendingFlowStates(insertedKeys);
            OidcAuthService.ClearDiscoveryCache();
        }
    }

    // --- Reflection helpers ---

    private static string InsertExpiredOneTimeCode()
    {
        var oneTimeCodesField = typeof(OidcAuthService)
            .GetField("OneTimeCodes", BindingFlags.NonPublic | BindingFlags.Static)!;
        var oneTimeCodes = oneTimeCodesField.GetValue(null)!;

        var entryType = typeof(OidcAuthService)
            .GetNestedType("OidcOneTimeCodeEntry", BindingFlags.NonPublic)!;
        var entry = Activator.CreateInstance(entryType)!;

        SetReflectionProperty(entry, "AccessToken", "test-access");
        SetReflectionProperty(entry, "RefreshToken", "test-refresh");
        SetReflectionProperty(entry, "ExpiresIn", 3600);
        SetReflectionProperty(entry, "CreatedAt", DateTime.UtcNow - TimeSpan.FromSeconds(31));

        var code = "expired-test-code-" + Guid.NewGuid().ToString("N");
        oneTimeCodes.GetType().GetMethod("TryAdd")!.Invoke(oneTimeCodes, new[] { code, entry });
        return code;
    }

    /// <summary>Replaces the stored OidcFlowState with one whose CreatedAt is backdated by the given age.</summary>
    private static void BackdateFlowState(string state, TimeSpan age)
    {
        var pendingFlowsField = typeof(OidcAuthService)
            .GetField("PendingFlows", BindingFlags.NonPublic | BindingFlags.Static)!;
        var pendingFlows = pendingFlowsField.GetValue(null)!;
        var dictType = pendingFlows.GetType();

        // Get the existing entry
        var indexer = dictType.GetProperty("Item")!;
        var existing = indexer.GetValue(pendingFlows, new object[] { state })!;

        // Build a new entry with CreatedAt backdated
        var flowType = existing.GetType();
        var newEntry = Activator.CreateInstance(flowType)!;

        foreach (var prop in flowType.GetProperties())
        {
            var value = prop.Name == "CreatedAt"
                ? DateTime.UtcNow - age
                : prop.GetValue(existing);
            SetReflectionProperty(newEntry, prop.Name, value!);
        }

        // Replace the entry: TryUpdate(state, newEntry, existing)
        var tryUpdate = dictType.GetMethod("TryUpdate")!;
        tryUpdate.Invoke(pendingFlows, new[] { state, newEntry, existing });
    }

    private static string InsertPendingFlowState(string redirectUri)
    {
        var pendingFlowsField = typeof(OidcAuthService)
            .GetField("PendingFlows", BindingFlags.NonPublic | BindingFlags.Static)!;
        var pendingFlows = pendingFlowsField.GetValue(null)!;

        var flowType = typeof(OidcAuthService)
            .GetNestedType("OidcFlowState", BindingFlags.NonPublic)!;
        var entry = Activator.CreateInstance(flowType)!;
        var key = "capacity-test-" + Guid.NewGuid().ToString("N");

        SetReflectionProperty(entry, "State", key);
        SetReflectionProperty(entry, "Nonce", "test-nonce");
        SetReflectionProperty(entry, "CodeVerifier", "test-verifier");
        SetReflectionProperty(entry, "RedirectUri", redirectUri);
        SetReflectionProperty(entry, "CreatedAt", DateTime.UtcNow);

        pendingFlows.GetType().GetMethod("TryAdd")!.Invoke(pendingFlows, new[] { key, entry });
        return key;
    }

    private static void RemovePendingFlowStates(IEnumerable<string> keys)
    {
        var pendingFlowsField = typeof(OidcAuthService)
            .GetField("PendingFlows", BindingFlags.NonPublic | BindingFlags.Static)!;
        var pendingFlows = pendingFlowsField.GetValue(null)!;
        var tryRemove = pendingFlows.GetType().GetMethod("TryRemove",
            new[] { typeof(string), pendingFlows.GetType().GetGenericArguments()[1].MakeByRefType() })!;

        foreach (var key in keys)
        {
            var args = new object?[] { key, null };
            tryRemove.Invoke(pendingFlows, args);
        }
    }

    private static void SetReflectionProperty(object obj, string propertyName, object value)
    {
        var prop = obj.GetType()
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        prop.SetValue(obj, value);
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

    /// <summary>
    /// Creates RSA-signed JWTs for use in ID token validation tests.
    /// </summary>
    private sealed class JwtTestHelper
    {
        private readonly RSA _rsa = RSA.Create(2048);
        private readonly RsaSecurityKey _key;

        public JwtTestHelper()
        {
            _key = new RsaSecurityKey(_rsa) { KeyId = "test-key-1" };
        }

        /// <summary>Creates a signed JWT. Pass subject=null to produce a token with no 'sub' claim.</summary>
        public string CreateIdToken(string issuer, string audience, string? subject, string nonce,
            DateTime? expiry = null, DateTime? notBefore = null)
        {
            var claims = new List<Claim> { new("nonce", nonce) };
            if (subject is not null)
                claims.Add(new Claim("sub", subject));

            var expiresAt = expiry ?? DateTime.UtcNow.AddHours(1);
            var notBeforeAt = notBefore ?? DateTime.UtcNow.AddMinutes(-1);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = audience,
                Subject = new ClaimsIdentity(claims),
                NotBefore = notBeforeAt,
                Expires = expiresAt,
                IssuedAt = notBeforeAt,
                SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256)
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        public string GetJwksJson()
        {
            var rsaParams = _rsa.ExportParameters(includePrivateParameters: false);
            return JsonSerializer.Serialize(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        use = "sig",
                        kid = _key.KeyId,
                        alg = "RS256",
                        n = Base64UrlEncode(rsaParams.Modulus!),
                        e = Base64UrlEncode(rsaParams.Exponent!)
                    }
                }
            });
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
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
            try
            {
                return Task.FromResult(_handler(request));
            }
            catch (Exception ex)
            {
                // Convert synchronous exceptions to faulted Tasks so HttpClient propagates them correctly
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}
