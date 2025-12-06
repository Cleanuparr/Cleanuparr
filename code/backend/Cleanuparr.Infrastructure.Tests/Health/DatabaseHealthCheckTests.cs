using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HealthCheckStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Cleanuparr.Infrastructure.Tests.Health;

/// <summary>
/// Basic tests for DatabaseHealthCheck.
/// Note: Full integration testing requires a real database since in-memory provider
/// doesn't support migrations (GetPendingMigrationsAsync).
/// </summary>
public class DatabaseHealthCheckTests : IDisposable
{
    private readonly Mock<ILogger<DatabaseHealthCheck>> _loggerMock;
    private DataContext? _dataContext;

    public DatabaseHealthCheckTests()
    {
        _loggerMock = new Mock<ILogger<DatabaseHealthCheck>>();
    }

    public void Dispose()
    {
        _dataContext?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dataContext = new DataContext(options);

        // Act
        var healthCheck = new DatabaseHealthCheck(_dataContext, _loggerMock.Object);

        // Assert
        Assert.NotNull(healthCheck);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task CheckHealthAsync_WhenDisposedContext_ReturnsUnhealthy()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var disposedContext = new DataContext(options);
        disposedContext.Dispose();

        var healthCheck = new DatabaseHealthCheck(disposedContext, _loggerMock.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthCheckStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthy_LogsError()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var disposedContext = new DataContext(options);
        disposedContext.Dispose();

        var healthCheck = new DatabaseHealthCheck(disposedContext, _loggerMock.Object);

        // Act
        await healthCheck.CheckHealthAsync(null!);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthy_DescriptionIndicatesFailure()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var disposedContext = new DataContext(options);
        disposedContext.Dispose();

        var healthCheck = new DatabaseHealthCheck(disposedContext, _loggerMock.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Contains("failed", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
