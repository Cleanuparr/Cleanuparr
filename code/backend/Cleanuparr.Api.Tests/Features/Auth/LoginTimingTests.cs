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
    public async Task Login_NoUserExists_StillCallsPasswordVerification()
    {
        _factory.TrackingPasswordService.Reset();

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "nouser",
            password = "SomePassword123!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _factory.TrackingPasswordService.VerifyPasswordCallCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact, TestPriority(1)]
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

    [Fact, TestPriority(2)]
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

    [Fact, TestPriority(3)]
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

    [Fact, TestPriority(4)]
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

    [Fact, TestPriority(5)]
    public async Task Login_TimingConsistency_InvalidAndValidUsernamesTakeSimilarTime()
    {
        const int iterations = 10;

        // Warm up the server and BCrypt static init
        await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "warmup",
            password = "WarmupPassword123!"
        });

        var invalidTimings = new List<long>(iterations);
        var validTimings = new List<long>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            // Alternate to avoid ordering bias
            var invalidSw = Stopwatch.StartNew();
            await _client.PostAsJsonAsync("/api/auth/login", new
            {
                username = $"nonexistent_{i}",
                password = "SomePassword123!"
            });
            invalidSw.Stop();
            invalidTimings.Add(invalidSw.ElapsedMilliseconds);

            var validSw = Stopwatch.StartNew();
            await _client.PostAsJsonAsync("/api/auth/login", new
            {
                username = "timingtest",
                password = "WrongPasswordForTiming!"
            });
            validSw.Stop();
            validTimings.Add(validSw.ElapsedMilliseconds);
        }

        var invalidMedian = Median(invalidTimings);
        var validMedian = Median(validTimings);

        // The invalid-username path must not be suspiciously fast
        invalidMedian.ShouldBeGreaterThan(50,
            $"Non-existent username median too fast ({invalidMedian}ms) — BCrypt may have been skipped");

        // Medians should be in the same ballpark
        var ratio = invalidMedian > validMedian
            ? (double)invalidMedian / validMedian
            : (double)validMedian / invalidMedian;

        ratio.ShouldBeLessThan(3.0,
            $"Timing difference too large: invalid median={invalidMedian}ms, valid median={validMedian}ms (ratio={ratio:F1}x)");
    }

    private static long Median(List<long> values)
    {
        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[mid - 1] + values[mid]) / 2
            : values[mid];
    }
}
