using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Tests that 2FA disable and regenerate accept a recovery code.
/// </summary>
[Collection("Auth Integration Tests")]
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class AccountControllerTwoFactorTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Username = "twofaadmin";
    private const string Password = "TwoFactorPassword123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static string? _accessToken;
    private static string _secret = "";
    private static List<string> _recoveryCodes = [];

    public AccountControllerTwoFactorTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        if (_accessToken is not null)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    [Fact, TestPriority(0)]
    public async Task Setup_CreateAccountAndLogin()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = Username,
            password = Password
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var completeResponse = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = Username,
            password = Password
        });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        _accessToken = body.GetProperty("tokens").GetProperty("accessToken").GetString();
        _accessToken.ShouldNotBeNullOrEmpty();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    [Fact, TestPriority(1)]
    public async Task Enable2fa_WithGeneratedTotpCode_TurnsTwoFactorOn()
    {
        await EnableTwoFactor();

        (await IsTwoFactorEnabled()).ShouldBeTrue();
    }

    [Fact, TestPriority(2)]
    public async Task Disable2fa_WithRecoveryCode_Succeeds()
    {
        var response = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
        {
            password = Password,
            totpCode = _recoveryCodes[0]
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsTwoFactorEnabled()).ShouldBeFalse();
    }

    [Fact, TestPriority(3)]
    public async Task Regenerate2fa_WithRecoveryCode_RotatesSecretAndCodes()
    {
        await EnableTwoFactor();

        var previousSecret = _secret;
        var previousCodes = _recoveryCodes;

        var response = await _client.PostAsJsonAsync("/api/account/2fa/regenerate", new
        {
            password = Password,
            totpCode = previousCodes[0]
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        _secret = body.GetProperty("secret").GetString()!;
        _recoveryCodes = ReadRecoveryCodes(body);

        _secret.ShouldNotBe(previousSecret);
        _recoveryCodes.Count.ShouldBe(10);
        _recoveryCodes.ShouldNotContain(previousCodes[0]);
    }

    [Fact, TestPriority(4)]
    public async Task Disable2fa_WithCodeFromRegeneratedBatch_Succeeds()
    {
        var response = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
        {
            password = Password,
            totpCode = _recoveryCodes[0]
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsTwoFactorEnabled()).ShouldBeFalse();
    }

    [Fact, TestPriority(5)]
    public async Task Disable2fa_WithRecoveryCodeAlreadyConsumedAtLogin_IsRejected()
    {
        await EnableTwoFactor();

        var anonymousClient = _factory.CreateClient();

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            username = Username,
            password = Password
        });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        loginBody.GetProperty("requiresTwoFactor").GetBoolean().ShouldBeTrue();

        var twoFactorResponse = await anonymousClient.PostAsJsonAsync("/api/auth/login/2fa", new
        {
            loginToken = loginBody.GetProperty("loginToken").GetString(),
            code = _recoveryCodes[0],
            isRecoveryCode = true
        });
        twoFactorResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var disableResponse = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
        {
            password = Password,
            totpCode = _recoveryCodes[0]
        });

        disableResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await IsTwoFactorEnabled()).ShouldBeTrue();

        await ClearLockout();
    }

    [Fact, TestPriority(6)]
    public async Task Disable2fa_WithUnknownCode_IsRejected()
    {
        var response = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
        {
            password = Password,
            totpCode = "ZZZZ-ZZZZ"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await IsTwoFactorEnabled()).ShouldBeTrue();

        await ClearLockout();
    }

    [Fact, TestPriority(7)]
    public async Task Regenerate2fa_WithUnknownCode_IsRejected()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/account/2fa/regenerate", new
        {
            password = Password,
            totpCode = "ZZZZ-ZZZZ"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await IsTwoFactorEnabled()).ShouldBeTrue();

        await ClearLockout();
    }

    [Fact, TestPriority(8)]
    public async Task Disable2fa_WithTotpCode_StillSucceeds()
    {
        var response = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
        {
            password = Password,
            totpCode = TotpTestHelper.GenerateTotpCode(_secret)
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsTwoFactorEnabled()).ShouldBeFalse();
    }

    [Fact, TestPriority(9)]
    public async Task Disable2fa_WithRepeatedBadCodes_EventuallyRateLimits()
    {
        await EnableTwoFactor();

        try
        {
            var first = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
            {
                password = Password,
                totpCode = "ZZZZ-ZZZZ"
            });

            first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
            firstBody.GetProperty("retryAfterSeconds").GetInt32().ShouldBeGreaterThan(0);

            var second = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
            {
                password = Password,
                totpCode = "ZZZZ-ZZZZ"
            });

            second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            (await IsTwoFactorEnabled()).ShouldBeTrue();
        }
        finally
        {
            await ClearLockout();
        }
    }

    [Fact, TestPriority(10)]
    public async Task Disable2fa_AfterLockoutCleared_ResetsTheCounterOnSuccess()
    {
        var response = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
        {
            password = Password,
            totpCode = TotpTestHelper.GenerateTotpCode(_secret)
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IsTwoFactorEnabled()).ShouldBeFalse();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersContext>();
        var user = await context.Users.FirstAsync();

        user.FailedLoginAttempts.ShouldBe(0);
        user.LockoutEnd.ShouldBeNull();
    }

    [Fact, TestPriority(11)]
    public async Task Login_WithTwoFactorEnabled_KeepsTheFailedAttemptCounter()
    {
        await EnableTwoFactor();

        try
        {
            await SeedFailedAttempts(3);

            await RequestLoginToken();

            (await ReadFailedAttempts()).ShouldBe(3);
        }
        finally
        {
            await ClearLockout();
            await DisableTwoFactor();
        }
    }

    [Fact, TestPriority(20)]
    public async Task Regenerate2fa_WhenIssuedConcurrently_AppliesOnce()
    {
        await EnableTwoFactor();

        string sharedCode = _recoveryCodes[0];

        const int attempts = 8;
        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, attempts).Select(_ =>
                _client.PostAsJsonAsync("/api/account/2fa/regenerate", new { password = Password, totpCode = sharedCode })));

        responses.Count(response => response.StatusCode is HttpStatusCode.OK).ShouldBe(1);
        responses.Count(response => response.StatusCode is HttpStatusCode.BadRequest).ShouldBe(attempts - 1);
        (await CountRecoveryCodes()).ShouldBe(10);

        HttpResponseMessage accepted = responses.First(response => response.StatusCode is HttpStatusCode.OK);
        JsonElement body = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        _secret = body.GetProperty("secret").GetString()!;
        _recoveryCodes = ReadRecoveryCodes(body);

        await ClearLockout();
    }

    [Fact, TestPriority(21)]
    public async Task Login2fa_WithSameRecoveryCodeConcurrently_SucceedsOnce()
    {
        string sharedCode = _recoveryCodes[0];

        string firstToken = await RequestLoginToken();
        string secondToken = await RequestLoginToken();

        HttpResponseMessage[] responses = await Task.WhenAll(
            _factory.CreateClient().PostAsJsonAsync("/api/auth/login/2fa", new { loginToken = firstToken, code = sharedCode, isRecoveryCode = true }),
            _factory.CreateClient().PostAsJsonAsync("/api/auth/login/2fa", new { loginToken = secondToken, code = sharedCode, isRecoveryCode = true }));

        responses.Count(response => response.StatusCode is HttpStatusCode.OK).ShouldBe(1);

        await ClearLockout();
    }

    [Fact, TestPriority(22)]
    public async Task Disable2fa_WithConcurrentBadCodes_LocksOutTheSecondRequest()
    {
        if (!await IsTwoFactorEnabled())
        {
            await EnableTwoFactor();
        }

        await ClearLockout();

        try
        {
            HttpResponseMessage[] responses = await Task.WhenAll(
                _client.PostAsJsonAsync("/api/account/2fa/disable", new { password = Password, totpCode = "ZZZZ-ZZZZ" }),
                _client.PostAsJsonAsync("/api/account/2fa/disable", new { password = Password, totpCode = "ZZZZ-ZZZZ" }));

            responses.Count(response => response.StatusCode is HttpStatusCode.BadRequest).ShouldBe(1);
            responses.Count(response => response.StatusCode is HttpStatusCode.TooManyRequests).ShouldBe(1);
            (await IsTwoFactorEnabled()).ShouldBeTrue();
        }
        finally
        {
            await ClearLockout();
        }
    }

    private async Task<string> RequestLoginToken()
    {
        HttpResponseMessage response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            username = Username,
            password = Password
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("requiresTwoFactor").GetBoolean().ShouldBeTrue();

        return body.GetProperty("loginToken").GetString()!;
    }

    private async Task<int> CountRecoveryCodes()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        UsersContext context = scope.ServiceProvider.GetRequiredService<UsersContext>();

        return await context.RecoveryCodes.CountAsync();
    }

    private async Task SeedFailedAttempts(int attempts)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        UsersContext context = scope.ServiceProvider.GetRequiredService<UsersContext>();
        User user = await context.Users.FirstAsync();

        user.FailedLoginAttempts = attempts;
        user.LockoutEnd = null;
        await context.SaveChangesAsync();
    }

    private async Task<int> ReadFailedAttempts()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        UsersContext context = scope.ServiceProvider.GetRequiredService<UsersContext>();

        return (await context.Users.AsNoTracking().FirstAsync()).FailedLoginAttempts;
    }

    private async Task ClearLockout()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersContext>();
        var user = await context.Users.FirstAsync();

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await context.SaveChangesAsync();
    }

    private async Task EnableTwoFactor()
    {
        var enableResponse = await _client.PostAsJsonAsync("/api/account/2fa/enable", new
        {
            password = Password
        });
        enableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await enableResponse.Content.ReadFromJsonAsync<JsonElement>();
        _secret = body.GetProperty("secret").GetString()!;
        _recoveryCodes = ReadRecoveryCodes(body);
        _recoveryCodes.Count.ShouldBe(10);

        var verifyResponse = await _client.PostAsJsonAsync("/api/account/2fa/enable/verify", new
        {
            code = TotpTestHelper.GenerateTotpCode(_secret)
        });
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task DisableTwoFactor()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/account/2fa/disable", new
        {
            password = Password,
            totpCode = TotpTestHelper.GenerateTotpCode(_secret)
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<bool> IsTwoFactorEnabled()
    {
        var response = await _client.GetAsync("/api/account");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("twoFactorEnabled").GetBoolean();
    }

    private static List<string> ReadRecoveryCodes(JsonElement body)
    {
        return body.GetProperty("recoveryCodes")
            .EnumerateArray()
            .Select(code => code.GetString()!)
            .ToList();
    }
}
