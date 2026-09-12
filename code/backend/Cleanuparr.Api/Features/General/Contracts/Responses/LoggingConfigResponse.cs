using Cleanuparr.Persistence.Models.Configuration.General;
using Serilog.Events;

namespace Cleanuparr.Api.Features.General.Contracts.Responses;

public sealed record LoggingConfigResponse
{
    public LogEventLevel Level { get; init; }

    public ushort RollingSizeMB { get; init; }

    public ushort RetainedFileCount { get; init; }

    public ushort TimeLimitHours { get; init; }

    public bool ArchiveEnabled { get; init; }

    public ushort ArchiveRetainedCount { get; init; }

    public ushort ArchiveTimeLimitHours { get; init; }

    public static LoggingConfigResponse From(LoggingConfig config) => new()
    {
        Level = config.Level,
        RollingSizeMB = config.RollingSizeMB,
        RetainedFileCount = config.RetainedFileCount,
        TimeLimitHours = config.TimeLimitHours,
        ArchiveEnabled = config.ArchiveEnabled,
        ArchiveRetainedCount = config.ArchiveRetainedCount,
        ArchiveTimeLimitHours = config.ArchiveTimeLimitHours,
    };
}
