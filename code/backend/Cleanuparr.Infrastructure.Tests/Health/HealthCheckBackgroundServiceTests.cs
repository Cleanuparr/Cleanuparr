using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Health;

public class HealthCheckBackgroundServiceTests : IDisposable
{
    private readonly Mock<ILogger<HealthCheckBackgroundService>> _loggerMock;
    private readonly Mock<IHealthCheckService> _healthCheckServiceMock;
    private HealthCheckBackgroundService? _service;

    public HealthCheckBackgroundServiceTests()
    {
        _loggerMock = new Mock<ILogger<HealthCheckBackgroundService>>();
        _healthCheckServiceMock = new Mock<IHealthCheckService>();
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    private HealthCheckBackgroundService CreateService()
    {
        _service = new HealthCheckBackgroundService(
            _loggerMock.Object,
            _healthCheckServiceMock.Object);
        return _service;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenCancelledImmediately_StopsGracefully()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        await service.StartAsync(cts.Token);
        await service.StopAsync(CancellationToken.None);

        // Assert - Should not throw
    }

    [Fact]
    public async Task ExecuteAsync_CallsCheckAllClientsHealthAsync()
    {
        // Arrange
        var service = CreateService();
        var healthResults = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("Client1") }
        };

        _healthCheckServiceMock
            .Setup(s => s.CheckAllClientsHealthAsync())
            .ReturnsAsync(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        // Give it some time to execute at least once
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _healthCheckServiceMock.Verify(s => s.CheckAllClientsHealthAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllClientsHealthy_LogsDebugMessage()
    {
        // Arrange
        var service = CreateService();
        var healthResults = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("Client1") },
            { Guid.NewGuid(), CreateHealthyStatus("Client2") }
        };

        _healthCheckServiceMock
            .Setup(s => s.CheckAllClientsHealthAsync())
            .ReturnsAsync(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Check that debug log was called (all healthy)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("healthy")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSomeClientsUnhealthy_LogsWarningMessage()
    {
        // Arrange
        var service = CreateService();
        var healthResults = new Dictionary<Guid, HealthStatus>
        {
            { Guid.NewGuid(), CreateHealthyStatus("Client1") },
            { Guid.NewGuid(), CreateUnhealthyStatus("Client2", "Connection failed") }
        };

        _healthCheckServiceMock
            .Setup(s => s.CheckAllClientsHealthAsync())
            .ReturnsAsync(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Check that warning log was called for unhealthy clients
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unhealthy")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHealthCheckThrows_LogsErrorAndContinues()
    {
        // Arrange
        var service = CreateService();
        var callCount = 0;

        _healthCheckServiceMock
            .Setup(s => s.CheckAllClientsHealthAsync())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new Exception("Health check failed");
                }
                return new Dictionary<Guid, HealthStatus>
                {
                    { Guid.NewGuid(), CreateHealthyStatus("Client1") }
                };
            });

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Error should be logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error performing periodic health check")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoClients_HandlesEmptyResults()
    {
        // Arrange
        var service = CreateService();
        var healthResults = new Dictionary<Guid, HealthStatus>();

        _healthCheckServiceMock
            .Setup(s => s.CheckAllClientsHealthAsync())
            .ReturnsAsync(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should handle gracefully
        _healthCheckServiceMock.Verify(s => s.CheckAllClientsHealthAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_LogsDetailedInfoForUnhealthyClients()
    {
        // Arrange
        var service = CreateService();
        var unhealthyClientId = Guid.NewGuid();
        var healthResults = new Dictionary<Guid, HealthStatus>
        {
            { unhealthyClientId, CreateUnhealthyStatus("UnhealthyClient", "Connection timeout") }
        };

        _healthCheckServiceMock
            .Setup(s => s.CheckAllClientsHealthAsync())
            .ReturnsAsync(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should log details about the unhealthy client
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UnhealthyClient") ||
                                              v.ToString()!.Contains("Connection timeout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public async Task StartAsync_StartsBackgroundService()
    {
        // Arrange
        _healthCheckServiceMock
            .Setup(s => s.CheckAllClientsHealthAsync())
            .ReturnsAsync(new Dictionary<Guid, HealthStatus>());

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);

        // Assert
        Assert.NotNull(service);

        // Cleanup
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_StopsGracefully()
    {
        // Arrange
        _healthCheckServiceMock
            .Setup(s => s.CheckAllClientsHealthAsync())
            .ReturnsAsync(new Dictionary<Guid, HealthStatus>());

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        // Act
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should log stop message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
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
            ResponseTime = TimeSpan.FromMilliseconds(50),
            ClientTypeName = DownloadClientTypeName.qBittorrent
        };
    }

    private static HealthStatus CreateUnhealthyStatus(string clientName, string errorMessage)
    {
        return new HealthStatus
        {
            IsHealthy = false,
            ClientName = clientName,
            ClientId = Guid.NewGuid(),
            LastChecked = DateTime.UtcNow,
            ResponseTime = TimeSpan.Zero,
            ErrorMessage = errorMessage,
            ClientTypeName = DownloadClientTypeName.qBittorrent
        };
    }

    #endregion
}
