using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence.Models.Auth;
using Cleanuparr.Shared.Helpers;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Auth;

public sealed class JwtServiceTests : IDisposable
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan LoginTokenLifetime = TimeSpan.FromMinutes(5);

    // whole seconds, because a JWT "exp" claim carries no sub-second precision
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _configDir = Path.Combine(Path.GetTempPath(), $"cleanuparr-jwt-{Guid.NewGuid():N}");
    private readonly string _previousConfigPath = ConfigurationPathProvider.GetConfigPath();

    public JwtServiceTests()
    {
        Directory.CreateDirectory(_configDir);
        ConfigurationPathProvider.SetConfigPath(_configDir);
    }

    public void Dispose()
    {
        ConfigurationPathProvider.SetConfigPath(_previousConfigPath);

        if (Directory.Exists(_configDir))
        {
            Directory.Delete(_configDir, recursive: true);
        }
    }

    private static DateTime ExpiryOf(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;

    [Fact]
    public void GenerateAccessToken_ExpiresAnHourAfterItWasIssued()
    {
        User user = new() { Id = Guid.NewGuid(), Username = "admin", PasswordHash = "hash", TotpSecret = "secret", ApiKey = "key" };

        string token = new JwtService(new FakeTimeProvider(Now)).GenerateAccessToken(user);

        ExpiryOf(token).ShouldBe(Now.UtcDateTime.Add(AccessTokenLifetime));
    }

    [Fact]
    public void GenerateLoginToken_ExpiresFiveMinutesAfterItWasIssued()
    {
        string token = new JwtService(new FakeTimeProvider(Now)).GenerateLoginToken(Guid.NewGuid());

        ExpiryOf(token).ShouldBe(Now.UtcDateTime.Add(LoginTokenLifetime));
    }

    [Fact]
    public void GenerateLoginToken_UsesADifferentAudienceThanAccessTokens()
    {
        JwtService jwtService = new(new FakeTimeProvider(Now));
        User user = new() { Id = Guid.NewGuid(), Username = "admin", PasswordHash = "hash", TotpSecret = "secret", ApiKey = "key" };
        JwtSecurityTokenHandler handler = new();

        IEnumerable<string> loginAudiences = handler.ReadJwtToken(jwtService.GenerateLoginToken(user.Id)).Audiences;
        IEnumerable<string> accessAudiences = handler.ReadJwtToken(jwtService.GenerateAccessToken(user)).Audiences;

        loginAudiences.ShouldNotBeEmpty();
        loginAudiences.ShouldNotContain(accessAudiences.Single());
    }

    [Fact]
    public void ValidateLoginToken_ForALoginToken_ReturnsItsUserId()
    {
        // Validation checks expiry against the system clock
        JwtService jwtService = new(TimeProvider.System);
        Guid userId = Guid.NewGuid();

        jwtService.ValidateLoginToken(jwtService.GenerateLoginToken(userId)).ShouldBe(userId);
    }

    [Fact]
    public void ValidateLoginToken_ForAnAccessToken_ReturnsNull()
    {
        JwtService jwtService = new(TimeProvider.System);
        User user = new() { Id = Guid.NewGuid(), Username = "admin", PasswordHash = "hash", TotpSecret = "secret", ApiKey = "key" };

        jwtService.ValidateLoginToken(jwtService.GenerateAccessToken(user)).ShouldBeNull();
    }

    [Fact]
    public void ValidateLoginToken_ForALoginAudienceTokenWithAccessType_ReturnsNull()
    {
        JwtService jwtService = new(TimeProvider.System);
        JwtSecurityToken token = new(
            issuer: "Cleanuparr",
            audience: "Cleanuparr-login",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(JwtService.TokenTypeClaim, JwtService.AccessTokenType)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(jwtService.GetOrCreateSigningKey()), SecurityAlgorithms.HmacSha256));

        jwtService.ValidateLoginToken(new JwtSecurityTokenHandler().WriteToken(token)).ShouldBeNull();
    }
}
