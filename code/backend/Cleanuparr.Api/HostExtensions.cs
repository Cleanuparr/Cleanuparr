using System.Reflection;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api;

public static class HostExtensions
{
    public static IHost Init(this WebApplication app)
    {
        ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
        AppStatusSnapshot statusSnapshot = app.Services.GetRequiredService<AppStatusSnapshot>();

        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        string? formattedVersion = FormatVersion(version);

        if (statusSnapshot.UpdateCurrentVersion(formattedVersion, out _))
        {
            logger.LogDebug("App status current version set to {Version}", formattedVersion);
        }

        logger.LogInformation(
            version is null
                ? "Cleanuparr version not detected"
                : $"Cleanuparr {formattedVersion}"
        );
        
        logger.LogInformation("timezone: {tz}", TimeZoneInfo.Local.DisplayName);
        
        return app;
    }

    private static string? FormatVersion(Version? version)
    {
        if (version is null)
        {
            return null;
        }

        if (version.Build >= 0)
        {
            return $"v{version.Major}.{version.Minor}.{version.Build}";
        }

        return $"v{version.Major}.{version.Minor}";
    }

    public static async Task<WebApplicationBuilder> InitAsync(this WebApplicationBuilder builder)
    {
        // Apply events db migrations
        await using var eventsContext = EventsContext.CreateStaticInstance();
        if ((await eventsContext.Database.GetPendingMigrationsAsync()).Any())
        {
            await eventsContext.Database.MigrateAsync();
        }

        // Apply data db migrations
        await using var configContext = DataContext.CreateStaticInstance();
        if ((await configContext.Database.GetPendingMigrationsAsync()).Any())
        {
            await configContext.Database.MigrateAsync();
        }
        
        return builder;
    }
}