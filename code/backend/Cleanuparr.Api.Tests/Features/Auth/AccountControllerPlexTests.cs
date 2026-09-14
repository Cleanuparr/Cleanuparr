using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Covers the account endpoints that stamp <see cref="User.UpdatedAt"/>.
/// Plex linking at setup and from settings, plus API key regeneration.
/// </summary>
[Collection("Auth Integration Tests")]
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class AccountControllerPlexTests : IClassFixture<AccountControllerPlexTests.PlexFactory>
{
    private const string Username = "plexadmin";
    private const string Password = "PlexPassword123!";

    private readonly PlexFactory _factory;
    private readonly HttpClient _client;

    private static string? _accessToken;

    public AccountControllerPlexTests(PlexFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        if (_accessToken is not null)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    [Fact, TestPriority(0)]
    public async Task SetupPlexVerify_LinksTheAccountBeforeSetupCompletes()
    {
        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = Username,
            password = Password,
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        PlexReturns("setup-token", "setup-plex-user");

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/setup/plex/verify", new { pinId = 1 });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("completed").GetBoolean().ShouldBeTrue();

        User user = await CurrentUser();
        user.PlexUsername.ShouldBe("setup-plex-user");
        user.UpdatedAt.ShouldBe(_factory.Clock.GetUtcNow());
    }

    [Fact, TestPriority(1)]
    public async Task CompleteSetupAndLogin()
    {
        HttpResponseMessage completeResponse = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = Username,
            password = Password,
        });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        _accessToken = body.GetProperty("tokens").GetProperty("accessToken").GetString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    [Fact, TestPriority(2)]
    public async Task RegenerateApiKey_StampsTheUser()
    {
        await Backdate();

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/account/api-key/regenerate", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("apiKey").GetString().ShouldNotBeNullOrEmpty();

        User user = await CurrentUser();
        user.UpdatedAt.ShouldBe(_factory.Clock.GetUtcNow());
    }

    [Fact, TestPriority(3)]
    public async Task VerifyPlexLink_StampsTheUser()
    {
        await Backdate();
        PlexReturns("account-token", "account-plex-user");

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/account/plex/link/verify", new { pinId = 2 });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        User user = await CurrentUser();
        user.PlexUsername.ShouldBe("account-plex-user");
        user.UpdatedAt.ShouldBe(_factory.Clock.GetUtcNow());
    }

    [Fact, TestPriority(4)]
    public async Task UnlinkPlex_StampsTheUser()
    {
        await Backdate();

        HttpResponseMessage response = await _client.DeleteAsync("/api/account/plex/link");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        User user = await CurrentUser();
        user.PlexUsername.ShouldBeNull();
        user.UpdatedAt.ShouldBe(_factory.Clock.GetUtcNow());
    }

    private void PlexReturns(string authToken, string plexUsername)
    {
        _factory.PlexAuthService.CheckPin(Arg.Any<int>())
            .Returns(new PlexPinCheckResult { Completed = true, AuthToken = authToken });
        _factory.PlexAuthService.GetAccount(authToken)
            .Returns(new PlexAccountInfo { AccountId = "42", Username = plexUsername, Email = "plex@example.com" });
    }

    /// <summary>
    /// Pushes the stamp into the past so the assertion proves the endpoint rewrote it.
    /// </summary>
    private async Task Backdate()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await using UsersContext context = scope.ServiceProvider.GetRequiredService<UsersContext>();

        User user = await context.Users.FirstAsync();
        user.UpdatedAt = DateTimeOffset.UtcNow.AddDays(-30);
        await context.SaveChangesAsync();
    }

    private async Task<User> CurrentUser()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await using UsersContext context = scope.ServiceProvider.GetRequiredService<UsersContext>();

        return await context.Users.AsNoTracking().FirstAsync();
    }

    public sealed class PlexFactory : CustomWebApplicationFactory
    {
        public IPlexAuthService PlexAuthService { get; } = Substitute.For<IPlexAuthService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlexAuthService>();
                services.AddSingleton(PlexAuthService);
            });
        }
    }
}
