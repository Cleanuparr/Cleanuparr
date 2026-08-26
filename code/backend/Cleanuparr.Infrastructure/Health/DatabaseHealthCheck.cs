using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Health check that verifies database connectivity
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly UsersContext _usersContext;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        DataContext dataContext,
        EventsContext eventsContext,
        UsersContext usersContext,
        ILogger<DatabaseHealthCheck> logger)
    {
        _dataContext = dataContext;
        _eventsContext = eventsContext;
        _usersContext = usersContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            List<string> unknownMigrations = [];

            foreach ((string name, DatabaseFacade database) in Schemas())
            {
                if (!await database.CanConnectAsync(cancellationToken))
                {
                    return HealthCheckResult.Unhealthy($"Cannot connect to the {name} database");
                }

                unknownMigrations.AddRange(
                    (await database.GetAppliedMigrationsAsync(cancellationToken))
                    .Except(database.GetMigrations())
                    .Select(migration => $"{name}: {migration}"));
            }

            if (unknownMigrations.Count > 0)
            {
                _logger.LogWarning(
                    "Database was written by a newer version: {Migrations}",
                    string.Join(", ", unknownMigrations));

                return HealthCheckResult.Degraded($"Database has {unknownMigrations.Count} migration(s) this version does not ship");
            }

            return HealthCheckResult.Healthy("Database connection successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }

    private IEnumerable<(string Name, DatabaseFacade Database)> Schemas()
    {
        yield return ("data", _dataContext.Database);
        yield return ("events", _eventsContext.Database);
        yield return ("users", _usersContext.Database);
    }
}
