using System.IO.Compression;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Cleanuparr.Infrastructure.Logging;

/// <summary>
/// Manages logging configuration and provides dynamic log level control
/// </summary>
public static class LoggingConfigManager
{
    /// <summary>
    /// The level switch used to dynamically control log levels
    /// </summary>
    public static LoggingLevelSwitch LevelSwitch { get; } = new();
    
    /// <summary>
    /// Creates a logger configuration for startup before DI is available
    /// </summary>
    /// <returns>Configured LoggerConfiguration</returns>
    public static LoggerConfiguration CreateLoggerConfiguration()
    {
        using var context = DataContext.CreateStaticInstance();
        var config = context.GeneralConfigs.AsNoTracking().First();
        SetLogLevel(config.Log.Level);
        
        const string categoryTemplate = "{#if Category is not null} {Concat('[',Category,']'),CAT_PAD}{#end}";
        const string jobNameTemplate = "{#if JobName is not null} {Concat('[',JobName,']'),JOB_PAD}{#end}";

        const string consoleOutputTemplate = $"[{{@t:yyyy-MM-dd HH:mm:ss.fff}} {{@l:u3}}]{jobNameTemplate}{categoryTemplate} {{@m}}\n{{@x}}";
        const string fileOutputTemplate = $"{{@t:yyyy-MM-dd HH:mm:ss.fff zzz}} [{{@l:u3}}]{jobNameTemplate}{categoryTemplate} {{@m:lj}}\n{{@x}}";

        // Determine job name padding
        List<string> jobNames = [nameof(JobType.QueueCleaner), nameof(JobType.MalwareBlocker), nameof(JobType.DownloadCleaner)];
        int jobPadding = jobNames.Max(x => x.Length) + 2;

        // Determine instance name padding
        List<string> categoryNames = [
            InstanceType.Sonarr.ToString(),
            InstanceType.Radarr.ToString(),
            InstanceType.Lidarr.ToString(),
            InstanceType.Readarr.ToString(),
            InstanceType.Whisparr.ToString(),
            "SYSTEM"
        ];
        int catPadding = categoryNames.Max(x => x.Length) + 2;

        // Apply padding values to templates
        string consoleTemplate = consoleOutputTemplate
            .Replace("JOB_PAD", jobPadding.ToString())
            .Replace("CAT_PAD", catPadding.ToString());

        string fileTemplate = fileOutputTemplate
            .Replace("JOB_PAD", jobPadding.ToString())
            .Replace("CAT_PAD", catPadding.ToString());

        LoggerConfiguration logConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .Enrich.FromLogContext()
            .WriteTo.Console(new ExpressionTemplate(consoleTemplate, theme: TemplateTheme.Literate));
        
        // Create the logs directory
        string logsPath = Path.Combine(ConfigurationPathProvider.GetConfigPath(), "logs");
        if (!Directory.Exists(logsPath))
        {
            try
            {
                Directory.CreateDirectory(logsPath);
            }
            catch (Exception exception)
            {
                throw new Exception($"Failed to create logs directory | {logsPath}", exception);
            }
        }

        ArchiveHooks? archiveHooks = config.Log.ArchiveEnabled
            ? new ArchiveHooks(
                retainedFileCountLimit: config.Log.ArchiveRetainedCount,
                retainedFileTimeLimit: config.Log.ArchiveTimeLimitHours > 0 ? TimeSpan.FromHours(config.Log.ArchiveTimeLimitHours) : null,
                compressionLevel: CompressionLevel.SmallestSize
            )
            : null;
        
        // Add file sink with archive hooks
        logConfig.WriteTo.File(
            path: Path.Combine(logsPath, "cleanuparr-.txt"),
            formatter: new ExpressionTemplate(fileTemplate),
            fileSizeLimitBytes: config.Log.RollingSizeMB is 0 ? null : config.Log.RollingSizeMB * 1024L * 1024L,
            rollingInterval: RollingInterval.Day,
            rollOnFileSizeLimit: config.Log.RollingSizeMB > 0,
            retainedFileCountLimit: config.Log.RetainedFileCount is 0 ? null : config.Log.RetainedFileCount,
            retainedFileTimeLimit: config.Log.TimeLimitHours is 0 ? null : TimeSpan.FromHours(config.Log.TimeLimitHours),
            hooks: archiveHooks
        );

        // Add SignalR sink for real-time log updates
        logConfig.WriteTo.Sink(SignalRLogSink.Instance);

        // Apply standard overrides
        logConfig
            .MinimumLevel.Override("MassTransit", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Quartz", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Error)
            .Enrich.WithProperty("ApplicationName", "Cleanuparr");

        return logConfig;
    }
    
    /// <summary>
    /// Updates the global log level and persists the change to configuration
    /// </summary>
    /// <param name="level">The new log level</param>
    public static void SetLogLevel(LogEventLevel level)
    {
        // Change the level in the switch
        LevelSwitch.MinimumLevel = level;
    }

    /// <summary>
    /// Reconfigures the entire logging system with new settings
    /// </summary>
    /// <param name="config">The new general configuration</param>
    public static void ReconfigureLogging(GeneralConfig config)
    {
        try
        {
            // Create new logger configuration
            var newLoggerConfig = CreateLoggerConfiguration();
            
            // Apply the new configuration to the global logger
            Log.Logger = newLoggerConfig.CreateLogger();
            
            // Update the level switch with the new level
            LevelSwitch.MinimumLevel = config.Log.Level;
        }
        catch (Exception ex)
        {
            // Log the error but don't throw to avoid breaking the application
            Log.Error(ex, "Failed to reconfigure logger");
        }
    }
}
