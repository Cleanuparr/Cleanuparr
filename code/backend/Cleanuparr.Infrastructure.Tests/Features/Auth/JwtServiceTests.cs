using System.IdentityModel.Tokens.Jwt;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence.Models.Auth;
using Cleanuparr.Shared.Helpers;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Auth;

public sealed class JwtServiceTests : IDisposable
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan LoginTokenLifetime = TimeSpan.FromMinutes(5);

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

        string token = new JwtService(TimeProvider.System).GenerateAccessToken(user);

        ExpiryOf(token).ShouldBe(DateTime.UtcNow.Add(AccessTokenLifetime), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateLoginToken_ExpiresFiveMinutesAfterItWasIssued()
    {
        string token = new JwtService(TimeProvider.System).GenerateLoginToken(Guid.NewGuid());

        ExpiryOf(token).ShouldBe(DateTime.UtcNow.Add(LoginTokenLifetime), TimeSpan.FromSeconds(5));
    }
}
