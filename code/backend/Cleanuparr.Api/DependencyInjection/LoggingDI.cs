using Cleanuparr.Infrastructure.Logging;
using Serilog;

namespace Cleanuparr.Api.DependencyInjection;

public static class LoggingDI
{
    public static ILoggingBuilder AddLogging(this ILoggingBuilder builder)
    {
        Log.Logger = LoggingConfigManager
            .CreateLoggerConfiguration()
            .CreateLogger();
        
        return builder.ClearProviders().AddSerilog();
    }
}