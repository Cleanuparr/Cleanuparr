using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Health;

public class HealthCheckBackgroundServiceTests : IDisposable
{
    private readonly ILogger<HealthCheckBackgroundService> _logger;
    private readonly IHealthCheckService _healthCheckService;
    private HealthCheckBackgroundService? _service;

    public HealthCheckBackgroundServiceTests()
    {
        _logger = Substitute.For<ILogger<HealthCheckBackgroundService>>();
        _healthCheckService = Substitute.For<IHealthCheckService>();
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    private HealthCheckBackgroundService CreateService()
    {
        _service = new HealthCheckBackgroundService(
            _logger,
            _healthCheckService);
        return _service;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        service.ShouldNotBeNull();
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

        _healthCheckService
            .CheckAllClientsHealthAsync()
            .Returns(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        // Give it some time to execute at least once
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        await _healthCheckService.Received().CheckAllClientsHealthAsync();
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

        _healthCheckService
            .CheckAllClientsHealthAsync()
            .Returns(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Check that debug log was called (all healthy)
        _logger.ReceivedLogContainingAtLeastOnce(LogLevel.Debug, "healthy");
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

        _healthCheckService
            .CheckAllClientsHealthAsync()
            .Returns(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Check that warning log was called for unhealthy clients
        _logger.ReceivedLogContainingAtLeastOnce(LogLevel.Warning, "unhealthy");
    }

    [Fact]
    public async Task ExecuteAsync_WhenHealthCheckThrows_LogsErrorAndContinues()
    {
        // Arrange
        var service = CreateService();
        var callCount = 0;

        _healthCheckService
            .CheckAllClientsHealthAsync()
            .Returns(ci =>
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
        _logger.ReceivedLogContainingAtLeastOnce(LogLevel.Error, "Error performing periodic health check");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoClients_HandlesEmptyResults()
    {
        // Arrange
        var service = CreateService();
        var healthResults = new Dictionary<Guid, HealthStatus>();

        _healthCheckService
            .CheckAllClientsHealthAsync()
            .Returns(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should handle gracefully
        await _healthCheckService.Received().CheckAllClientsHealthAsync();
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

        _healthCheckService
            .CheckAllClientsHealthAsync()
            .Returns(healthResults);

        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should log details about the unhealthy client
        var matchingCalls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments().Length > 0 && c.GetArguments()[0] is LogLevel l && l == LogLevel.Warning)
            .Where(c => c.GetArguments().Length > 2 &&
                        (c.GetArguments()[2]?.ToString()?.Contains("UnhealthyClient") == true ||
                         c.GetArguments()[2]?.ToString()?.Contains("Connection timeout") == true))
            .ToList();
        matchingCalls.ShouldNotBeEmpty();
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public async Task StartAsync_StartsBackgroundService()
    {
        // Arrange
        _healthCheckService
            .CheckAllClientsHealthAsync()
            .Returns(new Dictionary<Guid, HealthStatus>());

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);

        // Assert
        service.ShouldNotBeNull();

        // Cleanup
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_StopsGracefully()
    {
        // Arrange
        _healthCheckService
            .CheckAllClientsHealthAsync()
            .Returns(new Dictionary<Guid, HealthStatus>());

        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        // Act
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should log stop message
        _logger.ReceivedLogContainingAtLeastOnce(LogLevel.Information, "stopped");
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
