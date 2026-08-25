using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Health;

/// <summary>
/// Health check that verifies database connectivity
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly DataContext _dataContext;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(DataContext dataContext, ILogger<DatabaseHealthCheck> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to execute a simple query to verify database connectivity
            var canConnect = await _dataContext.Database.CanConnectAsync(cancellationToken);
            
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            // Startup applies pending migrations before the web host exists.
            // A rollback leaves migrations this build does not ship.
            List<string> unknownMigrations =
                (await _dataContext.Database.GetAppliedMigrationsAsync(cancellationToken))
                .Except(_dataContext.Database.GetMigrations())
                .ToList();

            if (unknownMigrations.Count > 0)
            {
                _logger.LogWarning(
                    "Database was written by a newer version: {Migrations}",
                    string.Join(", ", unknownMigrations));

                return HealthCheckResult.Degraded(
                    $"Database has {unknownMigrations.Count} migration(s) this version does not ship");
            }

            return HealthCheckResult.Healthy("Database connection successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }
} 