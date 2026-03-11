using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Integration tests for the OIDC account linking flow (POST /api/account/oidc/link and
/// GET /api/account/oidc/link/callback). Uses a mock IOidcAuthService that tracks the
/// initiatorUserId passed from StartOidcLink so OidcLinkCallback can complete the flow.
/// </summary>
[Collection("Auth Integration Tests")]
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class AccountControllerOidcTests : IClassFixture<AccountControllerOidcTests.OidcLinkWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly OidcLinkWebApplicationFactory _factory;

    // Shared across ordered tests
    private static string? _accessToken;

    public AccountControllerOidcTests(OidcLinkWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        if (_accessToken is not null)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    [Fact, TestPriority(0)]
    public async Task Setup_CreateAccountAndComplete()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = "linkadmin",
            password = "LinkPassword123!"
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var completeResponse = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact, TestPriority(1)]
    public async Task Login_StoreAccessToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "linkadmin",
            password = "LinkPassword123!"
        });

        var bodyText = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Login failed. Body: {bodyText}");

        var body = JsonSerializer.Deserialize<JsonElement>(bodyText);
        body.TryGetProperty("requiresTwoFactor", out var rtf)
            .ShouldBeTrue($"Missing 'requiresTwoFactor' in body: {bodyText}");
        rtf.GetBoolean().ShouldBeFalse();
        // Tokens are nested: { "requiresTwoFactor": false, "tokens": { "accessToken": "..." } }
        _accessToken = body.GetProperty("tokens").GetProperty("accessToken").GetString();
        _accessToken.ShouldNotBeNullOrEmpty();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    [Fact, TestPriority(2)]
    public async Task OidcLink_WhenOidcDisabled_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/account/oidc/link", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().ShouldContain("OIDC is not enabled");
    }

    [Fact, TestPriority(3)]
    public async Task EnableOidcConfig_ViaDirectDbUpdate()
    {
        await _factory.EnableOidcAsync();

        var statusResponse = await _client.GetAsync("/api/auth/status");
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("oidcEnabled").GetBoolean().ShouldBeTrue();
    }

    [Fact, TestPriority(4)]
    public async Task OidcLink_WhenAuthenticated_ReturnsAuthorizationUrl()
    {
        var response = await _client.PostAsync("/api/account/oidc/link", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var authUrl = body.GetProperty("authorizationUrl").GetString();
        authUrl.ShouldNotBeNullOrEmpty();
        authUrl.ShouldContain("authorize");
    }

    [Fact, TestPriority(5)]
    public async Task OidcLinkCallback_WithErrorParam_RedirectsToSettingsWithError()
    {
        var response = await _client.GetAsync("/api/account/oidc/link/callback?error=access_denied");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("/settings/general");
        location.ShouldContain("oidc_link_error=failed");
    }

    [Fact, TestPriority(6)]
    public async Task OidcLinkCallback_MissingCodeOrState_RedirectsWithError()
    {
        var noParams = await _client.GetAsync("/api/account/oidc/link/callback");
        noParams.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        noParams.Headers.Location?.ToString().ShouldContain("oidc_link_error=failed");

        var onlyCode = await _client.GetAsync("/api/account/oidc/link/callback?code=some-code");
        onlyCode.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        onlyCode.Headers.Location?.ToString().ShouldContain("oidc_link_error=failed");
    }

    [Fact, TestPriority(7)]
    public async Task OidcLinkCallback_ValidFlow_SavesSubjectAndRedirectsToSuccess()
    {
        // First trigger StartOidcLink so the mock captures the initiatorUserId
        var linkResponse = await _client.PostAsync("/api/account/oidc/link", null);
        linkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now simulate the IdP callback with the mock's success state
        var callbackResponse = await _client.GetAsync(
            $"/api/account/oidc/link/callback?code=valid-code&state={MockOidcAuthService.LinkSuccessState}");

        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = callbackResponse.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("/settings/general");
        location.ShouldContain("oidc_link=success");
        location.ShouldNotContain("oidc_link_error");

        // Verify the subject was saved to config
        var savedSubject = await _factory.GetAuthorizedSubjectAsync();
        savedSubject.ShouldBe(MockOidcAuthService.LinkedSubject);
    }

    [Fact, TestPriority(8)]
    public async Task OidcLinkCallback_NoInitiatorUserId_RedirectsWithError()
    {
        var response = await _client.GetAsync(
            $"/api/account/oidc/link/callback?code=valid-code&state={MockOidcAuthService.NoInitiatorState}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.ShouldNotBeNull();
        location.ShouldContain("oidc_link_error=failed");
    }

    [Fact, TestPriority(9)]
    public async Task OidcLink_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Create a fresh unauthenticated client
        var unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await unauthClient.PostAsync("/api/account/oidc/link", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    #region Test Infrastructure

    public class OidcLinkWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _configDir;

        public OidcLinkWebApplicationFactory()
        {
            _configDir = Cleanuparr.Shared.Helpers.ConfigurationPathProvider.GetConfigPath();

            // Delete both databases (and their WAL sidecar files) so each test run starts clean.
            // users.db must be at the config path so CreateStaticInstance() (used by
            // SetupGuardMiddleware) reads the same file as the DI-injected UsersContext.
            // ClearAllPools() releases any SQLite connections held by the previous test factory's
            // connection pool, which would otherwise prevent file deletion on Windows or cause
            // SQLite to reconstruct a partial database from stale WAL files on other platforms.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var name in new[] {
                "users.db", "users.db-shm", "users.db-wal",
                "cleanuparr.db", "cleanuparr.db-shm", "cleanuparr.db-wal" })
            {
                var path = Path.Combine(_configDir, name);
                if (File.Exists(path))
                    try { File.Delete(path); } catch { /* best effort */ }
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<UsersContext>));
                if (descriptor != null) services.Remove(descriptor);

                var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(UsersContext));
                if (contextDescriptor != null) services.Remove(contextDescriptor);

                // Use the config-path db so SetupGuardMiddleware (CreateStaticInstance) sees
                // the same data as the DI-injected context. Apply the same naming conventions
                // as CreateStaticInstance() so the schemas match.
                var dbPath = Path.Combine(_configDir, "users.db");
                services.AddDbContext<UsersContext>(options =>
                {
                    options
                        .UseSqlite($"Data Source={dbPath}")
                        .UseLowerCaseNamingConvention()
                        .UseSnakeCaseNamingConvention();
                });

                var oidcDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOidcAuthService));
                if (oidcDescriptor != null) services.Remove(oidcDescriptor);

                services.AddSingleton<IOidcAuthService, MockOidcAuthService>();

                // Remove all hosted services (Quartz scheduler, BackgroundJobManager) to prevent
                // Quartz.Logging.LogProvider.ResolvedLogProvider (a cached Lazy<T>) from being accessed
                // with a disposed ILoggerFactory from the previous factory lifecycle.
                // Auth tests don't depend on background job scheduling, so this is safe.
                foreach (var hostedService in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                    services.Remove(hostedService);

                // Ensure DB is created using a minimal isolated context (not the full app DI container)
                // to avoid any residual static state contamination.
                using var db = new UsersContext(
                    new DbContextOptionsBuilder<UsersContext>()
                        .UseSqlite($"Data Source={dbPath}")
                        .UseLowerCaseNamingConvention()
                        .UseSnakeCaseNamingConvention()
                        .Options);
                db.Database.EnsureCreated();
            });
        }

        public async Task EnableOidcAsync()
        {
            using var scope = Services.CreateScope();
            var usersContext = scope.ServiceProvider.GetRequiredService<UsersContext>();

            var user = await usersContext.Users.FirstOrDefaultAsync();
            if (user is null)
            {
                return;
            }

            user.Oidc = new OidcConfig
            {
                Enabled = true,
                IssuerUrl = "https://mock-oidc-provider.test",
                ClientId = "test-client",
                ClientSecret = "test-secret",
                Scopes = "openid profile email",
                AuthorizedSubject = "initial-subject",
                ProviderName = "TestProvider"
            };

            await usersContext.SaveChangesAsync();
        }

        public async Task<string?> GetAuthorizedSubjectAsync()
        {
            using var scope = Services.CreateScope();
            var usersContext = scope.ServiceProvider.GetRequiredService<UsersContext>();

            var user = await usersContext.Users.AsNoTracking().FirstOrDefaultAsync();
            return user?.Oidc.AuthorizedSubject;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                foreach (var name in new[] {
                    "users.db", "users.db-shm", "users.db-wal",
                    "cleanuparr.db", "cleanuparr.db-shm", "cleanuparr.db-wal" })
                {
                    var path = Path.Combine(_configDir, name);
                    if (File.Exists(path))
                        try { File.Delete(path); } catch { /* best effort */ }
                }
            }
        }
    }

    private sealed class MockOidcAuthService : IOidcAuthService
    {
        public const string LinkSuccessState = "mock-link-success-state";
        public const string NoInitiatorState = "mock-no-initiator-state";
        public const string LinkedSubject = "newly-linked-subject-123";

        private string? _lastInitiatorUserId;
        private readonly ConcurrentDictionary<string, OidcTokenExchangeResult> _oneTimeCodes = new();

        public Task<OidcAuthorizationResult> StartAuthorization(string redirectUri, string? initiatorUserId = null)
        {
            _lastInitiatorUserId = initiatorUserId;
            return Task.FromResult(new OidcAuthorizationResult
            {
                AuthorizationUrl = $"https://mock-oidc-provider.test/authorize?state={LinkSuccessState}",
                State = LinkSuccessState
            });
        }

        public Task<OidcCallbackResult> HandleCallback(string code, string state, string redirectUri)
        {
            if (state == LinkSuccessState)
            {
                return Task.FromResult(new OidcCallbackResult
                {
                    Success = true,
                    Subject = LinkedSubject,
                    PreferredUsername = "linkuser",
                    Email = "link@example.com",
                    InitiatorUserId = _lastInitiatorUserId
                });
            }

            if (state == NoInitiatorState)
            {
                return Task.FromResult(new OidcCallbackResult
                {
                    Success = true,
                    Subject = LinkedSubject,
                    InitiatorUserId = null // No initiator — controller should redirect with error
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

        public OidcTokenExchangeResult? ExchangeOneTimeCode(string code) =>
            _oneTimeCodes.TryRemove(code, out var result) ? result : null;
    }

    #endregion
}
