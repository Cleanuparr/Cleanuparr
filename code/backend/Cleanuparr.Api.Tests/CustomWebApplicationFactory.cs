using Cleanuparr.Shared.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

// Integration tests share file-system state (config-dir used by SetupGuardMiddleware),
// so they must be run sequentially to avoid interference between factories.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Cleanuparr.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory that redirects all database contexts to an isolated temp directory
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _tempDir;

    public CustomWebApplicationFactory()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cleanuparr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        ConfigurationPathProvider.SetConfigPath(_tempDir);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove all hosted services (Quartz scheduler, BackgroundJobManager) to prevent
            // Quartz.Logging.LogProvider.ResolvedLogProvider (a cached Lazy<T>) from being accessed
            foreach (var hostedService in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
            {
                services.Remove(hostedService);
            }
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
