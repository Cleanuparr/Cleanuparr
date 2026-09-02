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
        string databaseName = Guid.NewGuid().ToString();
        _dataContext = InMemoryContext<DataContext>(databaseName);

        // Act
        var healthCheck = new DatabaseHealthCheck(
            _dataContext,
            InMemoryContext<EventsContext>(databaseName),
            InMemoryContext<UsersContext>(databaseName),
            _logger);

        // Assert
        healthCheck.ShouldNotBeNull();
    }

    #endregion

    #region Schema Version Tests

    [Fact]
    public async Task CheckHealthAsync_WithFullyMigratedDatabases_ReturnsHealthy()
    {
        await using MigratedSchemas schemas = await MigratedSchemas.CreateAsync();

        var healthCheck = schemas.BuildHealthCheck(_logger);
        HealthCheckResult result = await healthCheck.CheckHealthAsync(null!);

        result.Status.ShouldBe(HealthCheckStatus.Healthy);
    }

    [Theory]
    [InlineData("data")]
    [InlineData("events")]
    [InlineData("users")]
    public async Task CheckHealthAsync_WhenASchemaHasMigrationsThisBuildDoesNotShip_ReturnsDegraded(string schema)
    {
        await using MigratedSchemas schemas = await MigratedSchemas.CreateAsync();

        // What a rollback leaves behind: history rows naming a migration this build never had.
        await schemas.Context(schema).Database.ExecuteSqlRawAsync(
            """INSERT INTO "__EFMigrationsHistory" ("migration_id", "product_version") VALUES ('99999999999999_FromTheFuture', '99.0.0')""");

        var healthCheck = schemas.BuildHealthCheck(_logger);
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
        string databaseName = Guid.NewGuid().ToString();
        DataContext disposedContext = InMemoryContext<DataContext>(databaseName);
        disposedContext.Dispose();

        var healthCheck = new DatabaseHealthCheck(
            disposedContext,
            InMemoryContext<EventsContext>(databaseName),
            InMemoryContext<UsersContext>(databaseName),
            _logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthy_LogsError()
    {
        // Arrange
        string databaseName = Guid.NewGuid().ToString();
        DataContext disposedContext = InMemoryContext<DataContext>(databaseName);
        disposedContext.Dispose();

        var healthCheck = new DatabaseHealthCheck(
            disposedContext,
            InMemoryContext<EventsContext>(databaseName),
            InMemoryContext<UsersContext>(databaseName),
            _logger);

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
        string databaseName = Guid.NewGuid().ToString();
        DataContext disposedContext = InMemoryContext<DataContext>(databaseName);
        disposedContext.Dispose();

        var healthCheck = new DatabaseHealthCheck(
            disposedContext,
            InMemoryContext<EventsContext>(databaseName),
            InMemoryContext<UsersContext>(databaseName),
            _logger);

        // Act
        var result = await healthCheck.CheckHealthAsync(null!);

        // Assert
        result.Description.ShouldContain("failed", Case.Insensitive);
    }

    #endregion

    private static TContext InMemoryContext<TContext>(string databaseName)
        where TContext : DbContext
    {
        DbContextOptionsBuilder<TContext> optionsBuilder = new();
        optionsBuilder.UseInMemoryDatabase(databaseName: $"{databaseName}-{typeof(TContext).Name}");

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }

    /// <summary>
    /// The three schemas the app migrates at startup, each in its own file like production.
    /// </summary>
    private sealed class MigratedSchemas : IAsyncDisposable
    {
        private readonly List<SqliteTestDatabase> _databases = [];
        private DataContext _data = null!;
        private EventsContext _events = null!;
        private UsersContext _users = null!;

        public static async Task<MigratedSchemas> CreateAsync()
        {
            MigratedSchemas schemas = new();
            schemas._data = await schemas.MigrateAsync<DataContext>("data");
            schemas._events = await schemas.MigrateAsync<EventsContext>("events");
            schemas._users = await schemas.MigrateAsync<UsersContext>("users");

            return schemas;
        }

        public DbContext Context(string schema) => schema switch
        {
            "data" => _data,
            "events" => _events,
            "users" => _users,
            _ => throw new ArgumentOutOfRangeException(nameof(schema), schema, null),
        };

        public DatabaseHealthCheck BuildHealthCheck(ILogger<DatabaseHealthCheck> logger) =>
            new(_data, _events, _users, logger);

        public async ValueTask DisposeAsync()
        {
            await _data.DisposeAsync();
            await _events.DisposeAsync();
            await _users.DisposeAsync();

            foreach (SqliteTestDatabase database in _databases)
            {
                database.Dispose();
            }
        }

        private async Task<TContext> MigrateAsync<TContext>(string name)
            where TContext : DbContext
        {
            SqliteTestDatabase database = SqliteTestDatabase.Create($"dbhealth-{name}");
            _databases.Add(database);

            TContext context = database.CreateContext<TContext>();
            await context.Database.MigrateAsync();

            return context;
        }
    }
}
