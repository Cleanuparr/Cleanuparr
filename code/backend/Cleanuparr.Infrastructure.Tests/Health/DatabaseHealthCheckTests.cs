using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
    private readonly ILogger<DatabaseHealthCheck> _logger;
    private DataContext? _dataContext;

    public DatabaseHealthCheckTests()
    {
        _logger = Substitute.For<ILogger<DatabaseHealthCheck>>();
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
        var healthCheck = new DatabaseHealthCheck(_dataContext, _logger);

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

        var healthCheck = new DatabaseHealthCheck(disposedContext, _logger);

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

        var healthCheck = new DatabaseHealthCheck(disposedContext, _logger);

        // Act
        await healthCheck.CheckHealthAsync(null!);

        // Assert
        var errorCalls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments().Length > 0 && c.GetArguments()[0] is LogLevel l && l == LogLevel.Error)
            .ToList();
        Assert.NotEmpty(errorCalls);
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

        var healthCheck = new DatabaseHealthCheck(disposedContext, _logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!);

        // Assert
        Assert.Contains("failed", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
