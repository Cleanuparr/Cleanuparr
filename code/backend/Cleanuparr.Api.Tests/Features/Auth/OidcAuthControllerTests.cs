using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Integration tests for the OIDC authentication flow.
/// Uses a mock IOidcAuthService to simulate IdP behavior.
/// Tests are ordered to build on each other: setup → enable OIDC → test flow.
/// </summary>
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class OidcAuthControllerTests : IClassFixture<OidcAuthControllerTests.OidcWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly OidcWebApplicationFactory _factory;

    public OidcAuthControllerTests(OidcWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // We want to inspect redirects
        });
    }

    [Fact, TestPriority(0)]
    public async Task OidcStart_BeforeSetup_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/auth/oidc/start", null);

        // OIDC start is on /api/auth/ path (not blocked by SetupGuardMiddleware)
        // but the controller returns BadRequest because OIDC is not configured
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(1)]
    public async Task Setup_CreateAccountAndComplete()
    {
        // Create account
        var createResponse = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = "admin",
            password = "TestPassword123!"
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Complete setup (skip 2FA for this test suite)
        var completeResponse = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact, TestPriority(2)]
    public async Task OidcStart_WhenDisabled_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/auth/oidc/start", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("OIDC is not enabled");
    }

    [Fact, TestPriority(3)]
    public async Task OidcExchange_WhenDisabled_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/oidc/exchange", new
        {
            code = "some-random-code"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(4)]
    public async Task OidcCallback_WithErrorParam_RedirectsToLoginWithError()
    {
        var response = await _client.GetAsync("/api/auth/oidc/callback?error=access_denied");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("/auth/login");
        location.ShouldContain("oidc_error=provider_error");
    }

    [Fact, TestPriority(5)]
    public async Task OidcCallback_WithoutCodeOrState_RedirectsToLoginWithError()
    {
        var response = await _client.GetAsync("/api/auth/oidc/callback");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("oidc_error=invalid_request");
    }

    [Fact, TestPriority(6)]
    public async Task OidcCallback_WithOnlyCode_RedirectsToLoginWithError()
    {
        var response = await _client.GetAsync("/api/auth/oidc/callback?code=some-code");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("oidc_error=invalid_request");
    }

    [Fact, TestPriority(7)]
    public async Task OidcCallback_WithInvalidState_RedirectsToLoginWithError()
    {
        // Even with code and state, if the state is invalid the mock will return failure
        var response = await _client.GetAsync("/api/auth/oidc/callback?code=some-code&state=invalid-state");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("oidc_error=authentication_failed");
    }

    [Fact, TestPriority(8)]
    public async Task EnableOidcConfig_ViaDirectDbUpdate()
    {
        // Simulate enabling OIDC via direct DB manipulation (since we'd normally do this through settings UI)
        await _factory.EnableOidcAsync();

        // Verify auth status reflects OIDC enabled
        var response = await _client.GetAsync("/api/auth/status");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("oidcEnabled").GetBoolean().ShouldBeTrue();
        body.GetProperty("oidcProviderName").GetString().ShouldBe("TestProvider");
    }

    [Fact, TestPriority(9)]
    public async Task OidcStart_WhenEnabled_ReturnsAuthorizationUrl()
    {
        var response = await _client.PostAsync("/api/auth/oidc/start", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var authUrl = body.GetProperty("authorizationUrl").GetString();
        authUrl.ShouldNotBeNullOrEmpty();
        authUrl.ShouldContain("authorize");
    }

    [Fact, TestPriority(10)]
    public async Task OidcCallback_ValidFlow_RedirectsWithOneTimeCode()
    {
        // Use the mock's valid state to simulate a successful callback
        var response = await _client.GetAsync(
            $"/api/auth/oidc/callback?code=valid-auth-code&state={MockOidcAuthService.ValidState}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("/auth/oidc/callback");
        location.ShouldContain("code=");
        // Should NOT contain oidc_error
        location.ShouldNotContain("oidc_error");
    }

    [Fact, TestPriority(11)]
    public async Task OidcExchange_ValidOneTimeCode_ReturnsTokens()
    {
        // First, trigger a valid callback to get a one-time code
        var callbackResponse = await _client.GetAsync(
            $"/api/auth/oidc/callback?code=valid-auth-code&state={MockOidcAuthService.ValidState}");
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var location = callbackResponse.Headers.Location?.ToString();
        location.ShouldNotBeNull();

        // Extract the one-time code from the redirect URL
        var uri = new Uri("http://localhost" + location);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var oneTimeCode = queryParams["code"];
        oneTimeCode.ShouldNotBeNullOrEmpty();

        // Exchange the one-time code for tokens
        var exchangeResponse = await _client.PostAsJsonAsync("/api/auth/oidc/exchange", new
        {
            code = oneTimeCode
        });

        exchangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await exchangeResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("refreshToken").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("expiresIn").GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact, TestPriority(12)]
    public async Task OidcExchange_SameCodeTwice_SecondFails()
    {
        // First, trigger a valid callback
        var callbackResponse = await _client.GetAsync(
            $"/api/auth/oidc/callback?code=valid-auth-code&state={MockOidcAuthService.ValidState}");
        var location = callbackResponse.Headers.Location?.ToString()!;
        var uri = new Uri("http://localhost" + location);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var oneTimeCode = queryParams["code"]!;

        // First exchange succeeds
        var response1 = await _client.PostAsJsonAsync("/api/auth/oidc/exchange", new { code = oneTimeCode });
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second exchange with same code fails
        var response2 = await _client.PostAsJsonAsync("/api/auth/oidc/exchange", new { code = oneTimeCode });
        response2.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(13)]
    public async Task OidcExchange_InvalidCode_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/oidc/exchange", new
        {
            code = "completely-invalid-code"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(14)]
    public async Task OidcCallback_UnauthorizedSubject_RedirectsWithError()
    {
        // Use the mock's state that returns a different subject
        var response = await _client.GetAsync(
            $"/api/auth/oidc/callback?code=valid-auth-code&state={MockOidcAuthService.WrongSubjectState}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("oidc_error=unauthorized");
    }

    [Fact, TestPriority(15)]
    public async Task AuthStatus_IncludesOidcFields()
    {
        var response = await _client.GetAsync("/api/auth/status");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("setupCompleted").GetBoolean().ShouldBeTrue();
        body.GetProperty("oidcEnabled").GetBoolean().ShouldBeTrue();
        body.GetProperty("oidcProviderName").GetString().ShouldBe("TestProvider");
    }

    [Fact, TestPriority(16)]
    public async Task PasswordLogin_StillWorks_AfterOidcEnabled()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "TestPassword123!"
        });

        // Should succeed (no 2FA since we skipped it in setup)
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // No 2FA, so should have tokens directly
        body.GetProperty("requiresTwoFactor").GetBoolean().ShouldBeFalse();
    }

    [Fact, TestPriority(17)]
    public async Task OidcStatus_WhenPartiallyConfigured_ReturnsFalse()
    {
        // Enable OIDC but remove the authorized subject
        await _factory.SetOidcAuthorizedSubjectAsync("");

        var response = await _client.GetAsync("/api/auth/status");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // OIDC should show as disabled when there's no authorized subject
        body.GetProperty("oidcEnabled").GetBoolean().ShouldBeFalse();

        // Restore for subsequent tests
        await _factory.SetOidcAuthorizedSubjectAsync(MockOidcAuthService.AuthorizedSubject);
    }

    [Fact, TestPriority(18)]
    public async Task OidcExchange_RandomCode_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/oidc/exchange", new
        {
            code = "completely-random-nonexistent-code"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #region Test Infrastructure

    /// <summary>
    /// Custom factory that replaces IOidcAuthService with a mock for testing.
    /// </summary>
    public class OidcWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _tempDir;

        public OidcWebApplicationFactory()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"cleanuparr-oidc-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);

            // Clean up any existing DataContext DB from previous test runs.
            // DataContext.CreateStaticInstance() uses ConfigurationPathProvider which
            // resolves to {AppContext.BaseDirectory}/config/cleanuparr.db.
            // We need to ensure a clean state for our tests.
            var configDir = Cleanuparr.Shared.Helpers.ConfigurationPathProvider.GetConfigPath();
            var dataDbPath = Path.Combine(configDir, "cleanuparr.db");
            if (File.Exists(dataDbPath))
            {
                try { File.Delete(dataDbPath); } catch { /* best effort */ }
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Remove existing UsersContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<UsersContext>));
                if (descriptor != null) services.Remove(descriptor);

                var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(UsersContext));
                if (contextDescriptor != null) services.Remove(contextDescriptor);

                var dbPath = Path.Combine(_tempDir, "users.db");
                services.AddDbContext<UsersContext>(options =>
                {
                    options.UseSqlite($"Data Source={dbPath}");
                });

                // Replace IOidcAuthService with mock
                var oidcDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOidcAuthService));
                if (oidcDescriptor != null) services.Remove(oidcDescriptor);

                services.AddSingleton<IOidcAuthService, MockOidcAuthService>();

                // Ensure DB is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<UsersContext>();
                db.Database.EnsureCreated();
            });
        }

        /// <summary>
        /// Enables OIDC in the DataContext config database.
        /// Uses CreateStaticInstance() which reads from ConfigurationPathProvider.GetConfigPath().
        /// </summary>
        public async Task EnableOidcAsync()
        {
            await using var dataContext = DataContext.CreateStaticInstance();
            dataContext.Database.EnsureCreated();

            var config = await dataContext.GeneralConfigs.FirstOrDefaultAsync();
            if (config is null)
            {
                config = new GeneralConfig
                {
                    Id = Guid.NewGuid(),
                    IgnoredDownloads = [],
                    Log = new LoggingConfig()
                };
                dataContext.GeneralConfigs.Add(config);
            }

            config.Auth.Oidc = new OidcConfig
            {
                Enabled = true,
                IssuerUrl = "https://mock-oidc-provider.test",
                ClientId = "test-client",
                ClientSecret = "test-secret",
                Scopes = "openid profile email",
                AuthorizedSubject = MockOidcAuthService.AuthorizedSubject,
                ProviderName = "TestProvider"
            };

            await dataContext.SaveChangesAsync();
        }

        public async Task SetOidcAuthorizedSubjectAsync(string subject)
        {
            await using var dataContext = DataContext.CreateStaticInstance();
            var config = await dataContext.GeneralConfigs.FirstOrDefaultAsync();
            if (config is not null)
            {
                config.Auth.Oidc.AuthorizedSubject = subject;
                await dataContext.SaveChangesAsync();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
            }
        }
    }

    /// <summary>
    /// Mock OIDC auth service that simulates IdP behavior without network calls.
    /// </summary>
    private sealed class MockOidcAuthService : IOidcAuthService
    {
        public const string ValidState = "mock-valid-state";
        public const string WrongSubjectState = "mock-wrong-subject-state";
        public const string AuthorizedSubject = "mock-authorized-subject-123";

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, OidcTokenExchangeResult> _oneTimeCodes = new();

        public Task<OidcAuthorizationResult> StartAuthorization(string redirectUri, string? initiatorUserId = null)
        {
            return Task.FromResult(new OidcAuthorizationResult
            {
                AuthorizationUrl = $"https://mock-oidc-provider.test/authorize?redirect_uri={Uri.EscapeDataString(redirectUri)}&state={ValidState}",
                State = ValidState
            });
        }

        public Task<OidcCallbackResult> HandleCallback(string code, string state, string redirectUri)
        {
            if (state == ValidState)
            {
                return Task.FromResult(new OidcCallbackResult
                {
                    Success = true,
                    Subject = AuthorizedSubject,
                    PreferredUsername = "testuser",
                    Email = "testuser@example.com"
                });
            }

            if (state == WrongSubjectState)
            {
                return Task.FromResult(new OidcCallbackResult
                {
                    Success = true,
                    Subject = "wrong-subject-that-doesnt-match",
                    PreferredUsername = "wronguser",
                    Email = "wrong@example.com"
                });
            }

            return Task.FromResult(new OidcCallbackResult
            {
                Success = false,
                Error = "Invalid or expired OIDC state"
            });
        }

        public string StoreOneTimeCode(string accessToken, string refreshToken, int expiresIn)
        {
            var code = Guid.NewGuid().ToString("N");
            _oneTimeCodes.TryAdd(code, new OidcTokenExchangeResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn
            });
            return code;
        }

        public OidcTokenExchangeResult? ExchangeOneTimeCode(string code)
        {
            return _oneTimeCodes.TryRemove(code, out var result) ? result : null;
        }
    }

    #endregion
}
