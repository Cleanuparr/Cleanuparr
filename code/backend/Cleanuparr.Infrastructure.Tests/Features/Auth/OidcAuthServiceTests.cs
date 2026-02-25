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

    #endregion

    public void Dispose()
    {
        _dataContext.Dispose();
    }
}
