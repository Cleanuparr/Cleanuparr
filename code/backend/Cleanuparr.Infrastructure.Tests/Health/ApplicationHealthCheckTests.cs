using Cleanuparr.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
        Assert.NotNull(healthCheck);
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
        Assert.Equal(HealthCheckStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_DescriptionIndicatesRunning()
    {
        // Arrange
        var healthCheck = new ApplicationHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Contains("running", result.Description, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal(HealthCheckStatus.Healthy, result.Status);
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
        Assert.Equal(HealthCheckStatus.Healthy, result.Status);
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
        Assert.Equal(HealthCheckStatus.Healthy, result1.Status);
        Assert.Equal(HealthCheckStatus.Healthy, result2.Status);
        Assert.Equal(HealthCheckStatus.Healthy, result3.Status);
    }

    #endregion
}
