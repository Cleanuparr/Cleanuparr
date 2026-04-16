using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;
using HealthCheckStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;
using HealthStatus = Cleanuparr.Infrastructure.Health.HealthStatus;

namespace Cleanuparr.Infrastructure.Tests.Health;

public class DownloadClientsHealthCheckTests
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<DownloadClientsHealthCheck> _logger;
    private readonly DownloadClientsHealthCheck _healthCheck;

    public DownloadClientsHealthCheckTests()
    {
        _healthCheckService = Substitute.For<IHealthCheckService>();
        _logger = Substitute.For<ILogger<DownloadClientsHealthCheck>>();
        _healthCheck = new DownloadClientsHealthCheck(_healthCheckService, _logger);
    }

    #region CheckHealthAsync Tests

    [Fact]
    public async Task CheckHealthAsync_WhenNoClientsConfigured_ReturnsHealthy()
    {
        // Arrange
        _healthCheckService
            .GetAllClientHealth()
            .Returns(new Dictionary<Guid, HealthStatus>());

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Healthy);
        result.Description.ShouldContain("No download clients configured");
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

        _healthCheckService
            .GetAllClientHealth()
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Healthy);
        result.Description.ShouldContain("All 3 download clients are healthy");
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

        _healthCheckService
            .GetAllClientHealth()
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Degraded);
        result.Description.ShouldContain("1/3");
        result.Description.ShouldContain("Client3");
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

        _healthCheckService
            .GetAllClientHealth()
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
        result.Description.ShouldContain("2/3");
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

        _healthCheckService
            .GetAllClientHealth()
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServiceThrows_ReturnsUnhealthy()
    {
        // Arrange
        _healthCheckService
            .GetAllClientHealth()
            .Throws(new Exception("Service error"));

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
        result.Description.ShouldContain("Download clients health check failed");
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

        _healthCheckService
            .GetAllClientHealth()
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Description.ShouldContain("BrokenClient1");
        result.Description.ShouldContain("BrokenClient2");
    }

    [Fact]
    public async Task CheckHealthAsync_WithSingleClient_HandlesCorrectly()
    {
        // Arrange - Single healthy client
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("OnlyClient") }
        };

        _healthCheckService
            .GetAllClientHealth()
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithSingleUnhealthyClient_ReturnsUnhealthy()
    {
        // Arrange - Single unhealthy client (1/1 > 50%)
        var clients = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateUnhealthyStatus("BrokenClient") }
        };

        _healthCheckService
            .GetAllClientHealth()
            .Returns(clients);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
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
