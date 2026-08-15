using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
