using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Tests that the login endpoint always runs BCrypt verification regardless of
/// username validity, preventing timing-based username enumeration.
/// </summary>
[Collection("Login Timing Tests")]
[TestCaseOrderer("Cleanuparr.Api.Tests.PriorityOrderer", "Cleanuparr.Api.Tests")]
public class LoginTimingTests : IClassFixture<TimingTestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TimingTestWebApplicationFactory _factory;

    public LoginTimingTests(TimingTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact, TestPriority(0)]
    public async Task Setup_CreateAccountAndComplete()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/auth/setup/account", new
        {
            username = "timingtest",
            password = "TimingTestPassword123!"
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var completeResponse = await _client.PostAsJsonAsync("/api/auth/setup/complete", new { });
        completeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact, TestPriority(1)]
    public async Task Login_ValidUsername_CallsPasswordVerification()
    {
        _factory.TrackingPasswordService.Reset();

        await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "timingtest",
            password = "TimingTestPassword123!"
        });

        _factory.TrackingPasswordService.VerifyPasswordCallCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact, TestPriority(2)]
    public async Task Login_NonexistentUsername_StillCallsPasswordVerification()
    {
        _factory.TrackingPasswordService.Reset();

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "doesnotexist",
            password = "SomePassword123!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _factory.TrackingPasswordService.VerifyPasswordCallCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact, TestPriority(3)]
    public async Task Login_LockedOutUser_StillCallsPasswordVerification()
    {
        // Trigger lockout by making several failed login attempts
        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", new
            {
                username = "timingtest",
                password = "WrongPassword!"
            });
        }

        _factory.TrackingPasswordService.Reset();

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "timingtest",
            password = "WrongPassword!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        _factory.TrackingPasswordService.VerifyPasswordCallCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact, TestPriority(4)]
    public async Task Login_TimingConsistency_InvalidAndValidUsernamesTakeSimilarTime()
    {
        // Warm up the server and BCrypt static init
        await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "warmup",
            password = "WarmupPassword123!"
        });

        // Measure response time for a non-existent username
        var invalidSw = Stopwatch.StartNew();
        await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "nonexistent_user",
            password = "SomePassword123!"
        });
        invalidSw.Stop();

        // Measure response time for the valid username
        var validSw = Stopwatch.StartNew();
        await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "timingtest",
            password = "WrongPasswordForTiming!"
        });
        validSw.Stop();

        var invalidMs = invalidSw.ElapsedMilliseconds;
        var validMs = validSw.ElapsedMilliseconds;

        // The invalid-username path must not be suspiciously fast (< 50ms would mean BCrypt was skipped)
        invalidMs.ShouldBeGreaterThan(50,
            $"Non-existent username returned too quickly ({invalidMs}ms) — BCrypt may have been skipped");

        // The ratio between the two should be reasonable
        var ratio = invalidMs > validMs
            ? (double)invalidMs / validMs
            : (double)validMs / invalidMs;

        ratio.ShouldBeLessThan(5.0,
            $"Timing difference too large: invalid={invalidMs}ms, valid={validMs}ms (ratio={ratio:F1}x)");
    }
}
