using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

// Integration tests share file-system state (config-dir users.db used by SetupGuardMiddleware),
// so we must run them sequentially to avoid interference between factories.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Cleanuparr.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory that uses an isolated SQLite database for each test fixture.
/// The database file is created in a temp directory so both DI and static contexts share the same data.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _tempDir;

    public CustomWebApplicationFactory()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cleanuparr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the existing UsersContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<UsersContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Also remove the DbContext registration itself
            var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(UsersContext));
            if (contextDescriptor != null) services.Remove(contextDescriptor);

            var dbPath = Path.Combine(_tempDir, "users.db");

            services.AddDbContext<UsersContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}");
            });

            // Remove all hosted services (Quartz scheduler, BackgroundJobManager) to prevent
            // Quartz.Logging.LogProvider.ResolvedLogProvider (a cached Lazy<T>) from being accessed
            // with a disposed ILoggerFactory from the previous factory lifecycle.
            // Auth tests don't depend on background job scheduling, so this is safe.
            foreach (var hostedService in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(hostedService);

            // Ensure DB is created using a minimal isolated context (not the full app DI container)
            // to avoid any residual static state contamination.
            using var db = new UsersContext(
                new DbContextOptionsBuilder<UsersContext>()
                    .UseSqlite($"Data Source={dbPath}")
                    .Options);
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { /* best effort cleanup */ }
        }
    }
}
