using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Integration tests for the authentication flow.
/// Uses a single shared factory to avoid static state conflicts.
/// Tests are ordered to build on each other: setup → login → protected endpoints.
/// </summary>
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact, TestPriority(0)]
    public async Task GetStatus_BeforeSetup_ReturnsNotCompleted()
    {
        var response = await _client.GetAsync("/api/auth/status");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("setupCompleted").GetBoolean().ShouldBeFalse();
    }

    [Fact, TestPriority(1)]
    public async Task Setup_CreateAccount_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = "admin",
            password = "TestPassword123!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact, TestPriority(2)]
    public async Task Setup_CreateDuplicateAccount_ReturnsConflict()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = "another",
            password = "TestPassword123!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact, TestPriority(3)]
    public async Task Setup_Generate2FA_ReturnsSecretAndRecoveryCodes()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/setup/2fa/generate", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("secret").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("qrCodeUri").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("recoveryCodes").GetArrayLength().ShouldBeGreaterThan(0);

        // Store the secret for the next test
        _totpSecret = body.GetProperty("secret").GetString()!;
    }

    [Fact, TestPriority(4)]
    public async Task Setup_Verify2FA_WithValidCode_Succeeds()
    {
        // If we don't have the secret from the previous test, generate it again
        if (string.IsNullOrEmpty(_totpSecret))
        {
            var genResponse = await _client.PostAsJsonAsync("/api/auth/setup/2fa/generate", new { });
            var genBody = await genResponse.Content.ReadFromJsonAsync<JsonElement>();
            _totpSecret = genBody.GetProperty("secret").GetString()!;
        }

        var code = GenerateTotpCode(_totpSecret);
        var response = await _client.PostAsJsonAsync("/api/auth/setup/2fa/verify", new { code });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact, TestPriority(5)]
    public async Task Setup_Complete_Succeeds()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact, TestPriority(6)]
    public async Task Login_ValidCredentials_RequiresTwoFactor()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "TestPassword123!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("requiresTwoFactor").GetBoolean().ShouldBeTrue();
        body.GetProperty("loginToken").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact, TestPriority(7)]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "WrongPassword!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact, TestPriority(8)]
    public async Task Login_BruteForce_ReturnsRetryAfter()
    {
        // Make multiple failed attempts
        for (int i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", new
            {
                username = "admin",
                password = "WrongPassword!"
            });
        }

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "WrongPassword!"
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            body.GetProperty("retryAfterSeconds").GetInt32().ShouldBeGreaterThan(0);
        }
        else
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            body.TryGetProperty("retryAfterSeconds", out var retry).ShouldBeTrue();
            retry.GetInt32().ShouldBeGreaterThan(0);
        }
    }

    [Fact, TestPriority(9)]
    public async Task ProtectedEndpoint_WithoutAuth_DeniesAccess()
    {
        var response = await _client.GetAsync("/api/account");

        // 401 (FallbackPolicy) or 403 (SetupGuardMiddleware) - both deny unauthenticated access
        new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }
            .ShouldContain(response.StatusCode);
    }

    [Fact, TestPriority(10)]
    public async Task HealthEndpoint_WithoutAuth_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #region TOTP helpers

    private static string _totpSecret = "";

    private static string GenerateTotpCode(string base32Secret)
    {
        var key = Base32Decode(base32Secret);
        var timestep = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds / 30;
        var timestepBytes = BitConverter.GetBytes(timestep);

        if (BitConverter.IsLittleEndian)
            Array.Reverse(timestepBytes);

        using var hmac = new System.Security.Cryptography.HMACSHA1(key);
        var hash = hmac.ComputeHash(timestepBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        return (binaryCode % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        base32 = base32.ToUpperInvariant().TrimEnd('=');

        var bits = new List<byte>();
        foreach (var c in base32)
        {
            var val = alphabet.IndexOf(c);
            if (val < 0) continue;
            for (var i = 4; i >= 0; i--)
                bits.Add((byte)((val >> i) & 1));
        }

        var bytes = new byte[bits.Count / 8];
        for (var i = 0; i < bytes.Length; i++)
        {
            for (var j = 0; j < 8; j++)
                bytes[i] = (byte)((bytes[i] << 1) | bits[i * 8 + j]);
        }

        return bytes;
    }

    #endregion
}
