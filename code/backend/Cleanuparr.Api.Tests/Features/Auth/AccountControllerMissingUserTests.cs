using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Covers the credential endpoints when a correctly signed access token names a user that does not exist.
/// Authentication accepts the token, so the action itself has to reject the request.
/// </summary>
[Collection("Auth Integration Tests")]
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class AccountControllerMissingUserTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Username = "ghostadmin";
    private const string Password = "GhostPassword123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AccountControllerMissingUserTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact, TestPriority(0)]
    public async Task Setup_CreateAccount()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = Username,
            password = Password
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var completeResponse = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact, TestPriority(1)]
    public async Task ChangePassword_ForAnUnknownUserId_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = TokenForAnUnknownUser();

        var response = await _client.PutAsJsonAsync("/api/account/password", new
        {
            currentPassword = Password,
            newPassword = "AnotherPassword456!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact, TestPriority(2)]
    public async Task ChangeUsername_ForAnUnknownUserId_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = TokenForAnUnknownUser();

        var response = await _client.PutAsJsonAsync("/api/account/username", new
        {
            currentPassword = Password,
            newUsername = "renamedadmin"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact, TestPriority(3)]
    public async Task UpdateOidcConfig_ForAnUnknownUserId_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = TokenForAnUnknownUser();

        var response = await _client.PutAsJsonAsync("/api/account/oidc", new
        {
            enabled = true
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private AuthenticationHeaderValue TokenForAnUnknownUser()
    {
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        // Signed with the running app's key, so authentication passes and the lookup inside the action is what fails
        string token = jwtService.GenerateAccessToken(new User
        {
            Id = Guid.NewGuid(),
            Username = "gone",
            PasswordHash = string.Empty,
            TotpSecret = string.Empty,
            ApiKey = string.Empty
        });

        return new AuthenticationHeaderValue("Bearer", token);
    }
}
