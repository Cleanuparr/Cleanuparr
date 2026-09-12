using System.Security.Claims;
using Cleanuparr.Api.Features.Auth;
using Cleanuparr.Api.Features.Auth.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Cleanuparr.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Records today's wire shape, including the masking of the secret.
/// </summary>
public sealed class OidcConfigResponseContractTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly UsersContext _usersContext;
    private readonly AccountController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public OidcConfigResponseContractTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        DbContextOptions<UsersContext> options = new DbContextOptionsBuilder<UsersContext>()
            .UseSqlite(_connection)
            .Options;

        _usersContext = new UsersContext(options);
        _usersContext.Database.EnsureCreated();

        _usersContext.Users.Add(new User
        {
            Id = _userId,
            Username = "admin",
            PasswordHash = "hash",
            TotpSecret = string.Empty,
            ApiKey = "0123456789abcdef",
            SetupCompleted = true,
            Oidc = new OidcConfig
            {
                Enabled = true,
                IssuerUrl = "https://oidc.test",
                ClientId = "test-client",
                ClientSecret = "super-secret",
                AuthorizedSubject = "subject-123",
                ProviderName = "TestProvider",
            },
        });
        _usersContext.SaveChanges();

        _controller = new AccountController(
            _usersContext,
            Substitute.For<IPasswordService>(),
            Substitute.For<ITotpService>(),
            Substitute.For<IPlexAuthService>(),
            Substitute.For<IOidcAuthService>(),
            new LoginAttemptTracker(_usersContext, NullLogger<LoginAttemptTracker>.Instance),
            NullLogger<AccountController>.Instance);
        ControllerTestContext.Attach(_controller);
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _userId.ToString())], "Test"));
    }

    public void Dispose()
    {
        _usersContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetOidcConfig_ReturnsTheDocumentedKeys()
    {
        IActionResult result = await _controller.GetOidcConfig();

        ResponseContract.Keys(result).ShouldBe(
        [
            "authorizedSubject",
            "clientId",
            "clientSecret",
            "enabled",
            "exclusiveMode",
            "issuerUrl",
            "providerName",
            "redirectUrl",
            "scopes",
        ]);
    }

    [Fact]
    public async Task GetOidcConfig_MasksTheClientSecret()
    {
        IActionResult result = await _controller.GetOidcConfig();

        ResponseContract.Body(result).GetProperty("clientSecret").GetString()
            .ShouldBe(SensitiveDataHelper.Placeholder);
    }
}
