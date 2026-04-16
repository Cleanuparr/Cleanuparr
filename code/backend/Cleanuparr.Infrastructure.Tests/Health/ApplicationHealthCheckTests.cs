using Cleanuparr.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;
using HealthCheckStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Cleanuparr.Infrastructure.Tests.Health;

public class ApplicationHealthCheckTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Act
        var healthCheck = new ApplicationHealthCheck();

        // Assert
        healthCheck.ShouldNotBeNull();
    }

    #endregion

    #region CheckHealthAsync Tests

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy()
    {
        // Arrange
        var healthCheck = new ApplicationHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_DescriptionIndicatesRunning()
    {
        // Arrange
        var healthCheck = new ApplicationHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Description.ShouldContain("running", Case.Insensitive);
    }

    [Fact]
    public async Task CheckHealthAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var healthCheck = new ApplicationHealthCheck();
        using var cts = new CancellationTokenSource();

        // Act
        var result = await healthCheck.CheckHealthAsync(null!, cts.Token);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithContext_CompletesSuccessfully()
    {
        // Arrange
        var healthCheck = new ApplicationHealthCheck();
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_MultipleCalls_AllReturnHealthy()
    {
        // Arrange
        var healthCheck = new ApplicationHealthCheck();

        // Act
        var result1 = await healthCheck.CheckHealthAsync(null!);
        var result2 = await healthCheck.CheckHealthAsync(null!);
        var result3 = await healthCheck.CheckHealthAsync(null!);

        // Assert
        result1.Status.ShouldBe(HealthCheckStatus.Healthy);
        result2.Status.ShouldBe(HealthCheckStatus.Healthy);
        result3.Status.ShouldBe(HealthCheckStatus.Healthy);
    }

    #endregion
}
