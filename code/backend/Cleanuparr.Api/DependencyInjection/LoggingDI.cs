using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Shared.Helpers;
using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Cleanuparr.Api.DependencyInjection;

public static class LoggingDI
{
    public static ILoggingBuilder AddLogging(this ILoggingBuilder builder)
    {
        Log.Logger = GetDefaultLoggerConfiguration().CreateLogger();
        
        return builder.ClearProviders().AddSerilog();
    }

    public static LoggerConfiguration GetDefaultLoggerConfiguration()
    {
        LoggerConfiguration logConfig = new();
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

        // Configure base logger with dynamic level control
        logConfig
            .MinimumLevel.Is(LogEventLevel.Information)
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
                throw new Exception($"Failed to create log directory | {logsPath}", exception);
            }
        }

        // Add main log file
        logConfig.WriteTo.File(
            path: Path.Combine(logsPath, "cleanuparr-.txt"),
            formatter: new ExpressionTemplate(fileTemplate),
            fileSizeLimitBytes: 10L * 1024 * 1024,
            rollingInterval: RollingInterval.Day,
            rollOnFileSizeLimit: true,
            shared: true
        );

        logConfig
            .MinimumLevel.Override("MassTransit", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Quartz", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Error)
            .Enrich.WithProperty("ApplicationName", "Cleanuparr");

        return logConfig;
    }
}