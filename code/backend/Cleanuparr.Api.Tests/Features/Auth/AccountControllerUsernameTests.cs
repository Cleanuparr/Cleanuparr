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
/// Integration tests for PUT /api/account/username.
/// </summary>
[Collection("Auth Integration Tests")]
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class AccountControllerUsernameTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Username = "renameadmin";
    private const string NewUsername = "renamedadmin";
    private const string Password = "RenamePassword123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static string? _accessToken;

    public AccountControllerUsernameTests(CustomWebApplicationFactory factory)
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
    public async Task ChangeUsername_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PutAsJsonAsync("/api/account/username", new
        {
            currentPassword = Password,
            newUsername = NewUsername
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact, TestPriority(2)]
    public async Task ChangeUsername_WithWrongPassword_ReturnsBadRequestAndKeepsUsername()
    {
        var response = await _client.PutAsJsonAsync("/api/account/username", new
        {
            currentPassword = "WrongPassword123!",
            newUsername = NewUsername
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetStoredUsername()).ShouldBe(Username);
    }

    [Fact, TestPriority(3)]
    public async Task ChangeUsername_TooShortAfterTrimming_ReturnsBadRequest()
    {
        var response = await _client.PutAsJsonAsync("/api/account/username", new
        {
            currentPassword = Password,
            newUsername = "   ab   "
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetStoredUsername()).ShouldBe(Username);
    }

    [Fact, TestPriority(4)]
    public async Task ChangeUsername_WithCurrentUsername_ReturnsBadRequest()
    {
        var response = await _client.PutAsJsonAsync("/api/account/username", new
        {
            currentPassword = Password,
            newUsername = Username
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(5)]
    public async Task ChangeUsername_ShorterThanThreeCharacters_IsRejectedByModelValidation()
    {
        var response = await _client.PutAsJsonAsync("/api/account/username", new
        {
            currentPassword = Password,
            newUsername = "ab"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetStoredUsername()).ShouldBe(Username);
    }

    [Fact, TestPriority(6)]
    public async Task ChangeUsername_WithValidPassword_TrimsStoresAndRevokesRefreshTokens()
    {
        var response = await _client.PutAsJsonAsync("/api/account/username", new
        {
            currentPassword = Password,
            newUsername = $"  {NewUsername}  "
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetStoredUsername()).ShouldBe(NewUsername);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersContext>();
        List<RefreshToken> tokens = await context.RefreshTokens.ToListAsync();

        tokens.ShouldNotBeEmpty();
        tokens.ShouldAllBe(t => t.RevokedAt != null);
    }

    [Fact, TestPriority(7)]
    public async Task Login_WithOldUsername_IsRejected()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            username = Username,
            password = Password
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact, TestPriority(8)]
    public async Task Login_WithNewUsername_Succeeds()
    {
        await ClearLockout();

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            username = NewUsername,
            password = Password
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<string> GetStoredUsername()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersContext>();
        User user = await context.Users.AsNoTracking().FirstAsync();

        return user.Username;
    }

    private async Task ClearLockout()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersContext>();
        User user = await context.Users.FirstAsync();

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await context.SaveChangesAsync();
    }
}
