using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HealthCheckStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;
using HealthStatus = Cleanuparr.Infrastructure.Health.HealthStatus;

namespace Cleanuparr.Infrastructure.Tests.Health;

public class DownloadClientsHealthCheckTests
{
    private readonly Mock<IHealthCheckService> _healthCheckServiceMock;
    private readonly Mock<ILogger<DownloadClientsHealthCheck>> _loggerMock;
    private readonly DownloadClientsHealthCheck _healthCheck;

    public DownloadClientsHealthCheckTests()
    {
        _healthCheckServiceMock = new Mock<IHealthCheckService>();
        _loggerMock = new Mock<ILogger<DownloadClientsHealthCheck>>();
        _healthCheck = new DownloadClientsHealthCheck(_healthCheckServiceMock.Object, _loggerMock.Object);
    }

    #region CheckHealthAsync Tests

    [Fact]
    public async Task CheckHealthAsync_WhenNoClientsConfigured_ReturnsHealthy()
    {
        // Arrange
        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Returns(new Dictionary<Guid, HealthStatus>());

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Healthy, result.Status);
        Assert.Contains("No download clients configured", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAllClientsHealthy_ReturnsHealthy()
    {
        // Arrange
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("Client1") },
            { Guid.NewGuid(), CreateHealthyStatus("Client2") },
            { Guid.NewGuid(), CreateHealthyStatus("Client3") }
        };

        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Healthy, result.Status);
        Assert.Contains("All 3 download clients are healthy", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSomeClientsUnhealthy_ReturnsDegraded()
    {
        // Arrange
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("Client1") },
            { Guid.NewGuid(), CreateHealthyStatus("Client2") },
            { Guid.NewGuid(), CreateUnhealthyStatus("Client3") }
        };

        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Degraded, result.Status);
        Assert.Contains("1/3", result.Description);
        Assert.Contains("Client3", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenMajorityUnhealthy_ReturnsUnhealthy()
    {
        // Arrange
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("Client1") },
            { Guid.NewGuid(), CreateUnhealthyStatus("Client2") },
            { Guid.NewGuid(), CreateUnhealthyStatus("Client3") }
        };

        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Unhealthy, result.Status);
        Assert.Contains("2/3", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAllUnhealthy_ReturnsUnhealthy()
    {
        // Arrange
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateUnhealthyStatus("Client1") },
            { Guid.NewGuid(), CreateUnhealthyStatus("Client2") }
        };

        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServiceThrows_ReturnsUnhealthy()
    {
        // Arrange
        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Throws(new Exception("Service error"));

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Unhealthy, result.Status);
        Assert.Contains("Download clients health check failed", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesUnhealthyClientNames()
    {
        // Arrange
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("HealthyClient") },
            { Guid.NewGuid(), CreateUnhealthyStatus("BrokenClient1") },
            { Guid.NewGuid(), CreateUnhealthyStatus("BrokenClient2") }
        };

        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Contains("BrokenClient1", result.Description);
        Assert.Contains("BrokenClient2", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WithSingleClient_HandlesCorrectly()
    {
        // Arrange - Single healthy client
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("OnlyClient") }
        };

        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WithSingleUnhealthyClient_ReturnsUnhealthy()
    {
        // Arrange - Single unhealthy client (1/1 > 50%)
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateUnhealthyStatus("BrokenClient") }
        };

        _healthCheckServiceMock
            .Setup(s => s.GetAllClientHealth())
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Unhealthy, result.Status);
    }

    #endregion

    #region Helper Methods

    private static HealthStatus CreateHealthyStatus(string clientName)
    {
        return new HealthStatus
        {
            IsHealthy = true,
            ClientName = clientName,
            ClientId = Guid.NewGuid(),
            LastChecked = DateTime.UtcNow,
            ClientTypeName = DownloadClientTypeName.qBittorrent
        };
    }

    private static HealthStatus CreateUnhealthyStatus(string clientName)
    {
        return new HealthStatus
        {
            IsHealthy = false,
            ClientName = clientName,
            ClientId = Guid.NewGuid(),
            LastChecked = DateTime.UtcNow,
            ErrorMessage = "Connection failed",
            ClientTypeName = DownloadClientTypeName.qBittorrent
        };
    }

    #endregion
}
