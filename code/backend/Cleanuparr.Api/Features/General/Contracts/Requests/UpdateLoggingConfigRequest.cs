using Cleanuparr.Persistence.Models.Configuration.General;
using Serilog.Events;

namespace Cleanuparr.Api.Features.General.Contracts.Requests;

public sealed record UpdateLoggingConfigRequest
{
    public LogEventLevel Level { get; init; } = LogEventLevel.Information;

    public ushort RollingSizeMB { get; init; } = 10;

    public ushort RetainedFileCount { get; init; } = 5;

    public ushort TimeLimitHours { get; init; } = 24;

    public bool ArchiveEnabled { get; init; } = true;

    public ushort ArchiveRetainedCount { get; init; } = 60;

    public ushort ArchiveTimeLimitHours { get; init; } = 24 * 30;

    public bool ApplyTo(LoggingConfig existingConfig)
    {
        bool levelChanged = existingConfig.Level != Level;
        bool otherPropertiesChanged =
            existingConfig.RollingSizeMB != RollingSizeMB ||
            existingConfig.RetainedFileCount != RetainedFileCount ||
            existingConfig.TimeLimitHours != TimeLimitHours ||
            existingConfig.ArchiveEnabled != ArchiveEnabled ||
            existingConfig.ArchiveRetainedCount != ArchiveRetainedCount ||
            existingConfig.ArchiveTimeLimitHours != ArchiveTimeLimitHours;

        existingConfig.Level = Level;
        existingConfig.RollingSizeMB = RollingSizeMB;
        existingConfig.RetainedFileCount = RetainedFileCount;
        existingConfig.TimeLimitHours = TimeLimitHours;
        existingConfig.ArchiveEnabled = ArchiveEnabled;
        existingConfig.ArchiveRetainedCount = ArchiveRetainedCount;
        existingConfig.ArchiveTimeLimitHours = ArchiveTimeLimitHours;

        existingConfig.Validate();

        LevelOnlyChange = levelChanged && !otherPropertiesChanged;

        return levelChanged || otherPropertiesChanged;
    }

    public bool LevelOnlyChange { get; private set; }
}
