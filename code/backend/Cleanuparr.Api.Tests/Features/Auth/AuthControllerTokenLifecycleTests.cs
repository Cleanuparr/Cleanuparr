using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Covers refresh-token rotation, expiry and logout, which all stamp the clock.
/// </summary>
[Collection("Auth Integration Tests")]
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class AuthControllerTokenLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Username = "tokenadmin";
    private const string Password = "TokenPassword123!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static string _refreshToken = "";

    public AuthControllerTokenLifecycleTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact, TestPriority(0)]
    public async Task Login_IssuesARefreshTokenThatExpiresInAWeek()
    {
        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = Username,
            password = Password,
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        HttpResponseMessage completeResponse = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        _refreshToken = await Login();

        RefreshToken issued = await NewestToken();
        issued.CreatedAt.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        issued.ExpiresAt.ShouldBe(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact, TestPriority(1)]
    public async Task Refresh_RevokesTheOldTokenAndIssuesANewOne()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = _refreshToken });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        string rotated = body.GetProperty("refreshToken").GetString()!;
        rotated.ShouldNotBe(_refreshToken);

        await using UsersContext context = Context(out IServiceScope scope);
        using (scope)
        {
            RefreshToken revoked = await context.RefreshTokens.SingleAsync(t => t.RevokedAt != null);
            revoked.RevokedAt!.Value.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        }

        _refreshToken = rotated;
    }

    [Fact, TestPriority(2)]
    public async Task Refresh_WithAnExpiredToken_IsRejected()
    {
        await using (UsersContext context = Context(out IServiceScope writeScope))
        {
            using (writeScope)
            {
                RefreshToken active = await context.RefreshTokens
                    .Where(t => t.RevokedAt == null)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstAsync();
                active.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
                await context.SaveChangesAsync();
            }
        }

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = _refreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact, TestPriority(3)]
    public async Task Logout_RevokesTheRefreshToken()
    {
        _refreshToken = await Login();

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = _refreshToken });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using UsersContext context = Context(out IServiceScope scope);
        using (scope)
        {
            RefreshToken revoked = await context.RefreshTokens
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            revoked.RevokedAt.ShouldNotBeNull();
            revoked.RevokedAt!.Value.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        }
    }

    private async Task<string> Login()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = Username,
            password = Password,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("tokens").GetProperty("refreshToken").GetString()!;
    }

    private async Task<RefreshToken> NewestToken()
    {
        await using UsersContext context = Context(out IServiceScope scope);
        using (scope)
        {
            return await context.RefreshTokens
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
        }
    }

    private UsersContext Context(out IServiceScope scope)
    {
        scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<UsersContext>();
    }
}
