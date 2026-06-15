using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Integration tests for POST /api/account/feature-views. Verifies that feature "first seen"
/// timestamps are recorded per user, that recording is idempotent, and that the endpoint
/// requires authentication.
/// </summary>
[Collection("Auth Integration Tests")]
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class AccountControllerFeatureViewsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static string? _accessToken;

    public AccountControllerFeatureViewsTests(CustomWebApplicationFactory factory)
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
            username = "featureadmin",
            password = "FeaturePassword123!"
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var completeResponse = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "featureadmin",
            password = "FeaturePassword123!"
        });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        _accessToken = body.GetProperty("tokens").GetProperty("accessToken").GetString();
        _accessToken.ShouldNotBeNullOrEmpty();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    [Fact, TestPriority(1)]
    public async Task RecordFeatureViews_NewIds_RecordsTimestampsAndReturnsMapWithAnchor()
    {
        var response = await _client.PostAsJsonAsync("/api/account/feature-views", new
        {
            featureIds = new[] { "feature-a", "feature-b" }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<FeatureViewsResponseDto>();
        body.ShouldNotBeNull();
        body.CreatedAt.ShouldNotBe(default);
        body.Views.ShouldContainKey("feature-a");
        body.Views.ShouldContainKey("feature-b");
        body.Views["feature-a"].Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact, TestPriority(2)]
    public async Task RecordFeatureViews_DuplicateId_IsIdempotentAndKeepsOriginalTimestamp()
    {
        var firstResponse = await _client.PostAsJsonAsync("/api/account/feature-views", new
        {
            featureIds = new[] { "feature-a" }
        });
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<FeatureViewsResponseDto>();
        var originalTimestamp = firstBody!.Views["feature-a"];

        var secondResponse = await _client.PostAsJsonAsync("/api/account/feature-views", new
        {
            featureIds = new[] { "feature-a" }
        });
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<FeatureViewsResponseDto>();

        secondBody!.Views["feature-a"].ShouldBe(originalTimestamp);
    }

    [Fact, TestPriority(3)]
    public async Task RecordFeatureViews_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var unauthClient = _factory.CreateClient();

        var response = await unauthClient.PostAsJsonAsync("/api/account/feature-views", new
        {
            featureIds = new[] { "feature-a" }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact, TestPriority(4)]
    public async Task RecordFeatureViews_TooManyIds_ReturnsBadRequest()
    {
        var tooMany = Enumerable.Range(0, 101).Select(i => $"feature-{i}").ToArray();

        var response = await _client.PostAsJsonAsync("/api/account/feature-views", new
        {
            featureIds = tooMany
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(5)]
    public async Task RecordFeatureViews_OverLengthId_IsSkipped()
    {
        var overLengthId = new string('x', 65);

        var response = await _client.PostAsJsonAsync("/api/account/feature-views", new
        {
            featureIds = new[] { "feature-ok", overLengthId }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<FeatureViewsResponseDto>();
        body.ShouldNotBeNull();
        body.Views.ShouldContainKey("feature-ok");
        body.Views.ShouldNotContainKey(overLengthId);
    }

    private sealed record FeatureViewsResponseDto
    {
        public DateTimeOffset CreatedAt { get; init; }
        public Dictionary<string, DateTimeOffset> Views { get; init; } = new();
    }
}
