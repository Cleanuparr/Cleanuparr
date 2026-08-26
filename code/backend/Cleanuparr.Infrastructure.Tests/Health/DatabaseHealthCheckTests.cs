using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using HealthCheckStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Cleanuparr.Infrastructure.Tests.Health;

/// <summary>
/// Basic tests for DatabaseHealthCheck.
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
        healthCheck.ShouldNotBeNull();
    }

    #endregion

    #region Schema Version Tests

    [Fact]
    public async Task CheckHealthAsync_WithAFullyMigratedDatabase_ReturnsHealthy()
    {
        using SqliteTestDatabase database = SqliteTestDatabase.Create("dbhealth");
        await using DataContext context = database.CreateContext<DataContext>();
        await context.Database.MigrateAsync();

        var healthCheck = new DatabaseHealthCheck(context, _logger);
        HealthCheckResult result = await healthCheck.CheckHealthAsync(null!);

        result.Status.ShouldBe(HealthCheckStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTheDatabaseHasMigrationsThisBuildDoesNotShip_ReturnsDegraded()
    {
        using SqliteTestDatabase database = SqliteTestDatabase.Create("dbhealth");
        await using DataContext context = database.CreateContext<DataContext>();
        await context.Database.MigrateAsync();

        // What a rollback leaves behind: history rows naming a migration this build never had.
        await context.Database.ExecuteSqlRawAsync(
            """INSERT INTO "__EFMigrationsHistory" ("migration_id", "product_version") VALUES ('99999999999999_FromTheFuture', '99.0.0')""");

        var healthCheck = new DatabaseHealthCheck(context, _logger);
        HealthCheckResult result = await healthCheck.CheckHealthAsync(null!);

        result.Status.ShouldBe(HealthCheckStatus.Degraded);

        // An operator needs the reason.
        result.Description.ShouldContain("1 migration(s)");
        _logger.ReceivedLogContaining(
            LogLevel.Warning, "Database was written by a newer version");
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
        result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
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
        errorCalls.ShouldNotBeEmpty();
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
        result.Description.ShouldContain("failed", Case.Insensitive);
    }

    #endregion
}
