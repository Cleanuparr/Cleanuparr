using System.Reflection;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api;

public static class HostExtensions
{
    public static async Task<IHost> InitAsync(this WebApplication app)
    {
        ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();

        Version? version = Assembly.GetExecutingAssembly().GetName().Version;

        logger.LogInformation(
            version is null
                ? "Cleanuparr version not detected"
                : $"Cleanuparr v{version.Major}.{version.Minor}.{version.Build}"
        );
        
        logger.LogInformation("timezone: {tz}", TimeZoneInfo.Local.DisplayName);
        
        return app;
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