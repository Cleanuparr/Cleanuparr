using Cleanuparr.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Controllers;

public sealed class HealthControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly HealthCheckService _healthCheckService = Substitute.For<HealthCheckService>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _controller = new HealthController(_healthCheckService, Substitute.For<ILogger<HealthController>>(), _timeProvider);
    }

    private void ReportsStatus(HealthStatus status)
    {
        HealthReportEntry entry = new(status, "probe", TimeSpan.FromMilliseconds(5), null, null);
        HealthReport report = new(new Dictionary<string, HealthReportEntry> { ["db"] = entry }, TimeSpan.FromMilliseconds(5));

        _healthCheckService
            .CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>?>(), Arg.Any<CancellationToken>())
            .Returns(report);
    }

    private void ProbeThrows()
    {
        _healthCheckService
            .CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("probe exploded"));
    }

    private static DateTimeOffset TimestampOf(IActionResult result)
    {
        object payload = result.ShouldBeAssignableTo<ObjectResult>()!.Value!;
        return (DateTimeOffset)payload.GetType().GetProperty("timestamp")!.GetValue(payload)!;
    }

    [Fact]
    public async Task GetHealth_StampsEveryOutcome()
    {
        ReportsStatus(HealthStatus.Healthy);
        TimestampOf(await _controller.GetHealth()).ShouldBe(Now);

        ReportsStatus(HealthStatus.Unhealthy);
        TimestampOf(await _controller.GetHealth()).ShouldBe(Now);

        ProbeThrows();
        TimestampOf(await _controller.GetHealth()).ShouldBe(Now);
    }

    [Fact]
    public async Task GetReadiness_StampsEveryOutcome()
    {
        ReportsStatus(HealthStatus.Healthy);
        TimestampOf(await _controller.GetReadiness()).ShouldBe(Now);

        // Degraded counts as not ready, and carries the failing entries.
        ReportsStatus(HealthStatus.Degraded);
        TimestampOf(await _controller.GetReadiness()).ShouldBe(Now);

        ProbeThrows();
        TimestampOf(await _controller.GetReadiness()).ShouldBe(Now);
    }

    [Fact]
    public async Task GetDetailedHealth_StampsEveryOutcome()
    {
        ReportsStatus(HealthStatus.Healthy);
        TimestampOf(await _controller.GetDetailedHealth()).ShouldBe(Now);

        ProbeThrows();
        TimestampOf(await _controller.GetDetailedHealth()).ShouldBe(Now);
    }
}
