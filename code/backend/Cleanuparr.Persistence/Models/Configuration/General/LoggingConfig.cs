using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Exceptions;
using Serilog;
using Serilog.Events;

namespace Cleanuparr.Persistence.Models.Configuration.General;

[ComplexType]
public sealed record LoggingConfig : IConfig
{
    public LogEventLevel Level { get; set; } = LogEventLevel.Information;

    public ushort RollingSizeMB { get; set; } = 10; // 0 = disabled
    
    public ushort RetainedFileCount { get; set; } = 5; // 0 = unlimited
    
    public ushort TimeLimitHours { get; set; } = 24; // 0 = unlimited

    // Archive Configuration  
    public bool ArchiveEnabled { get; set; } = true;
    
    public ushort ArchiveRetainedCount { get; set; } = 60; // 0 = unlimited
    
    public ushort ArchiveTimeLimitHours { get; set; } = 24 * 30; // 0 = unlimited

    public void Validate()
    {
        if (RollingSizeMB > 100)
        {
            throw new ValidationException("Log rolling size cannot exceed 100 MB");
        }

        if (RetainedFileCount > 50)
        {
            throw new ValidationException("Log retained file count cannot exceed 50");
        }
        
        if (TimeLimitHours > 1440) // 24 * 60
        {
            throw new ValidationException("Log time limit cannot exceed 60 days");
        }

        if (ArchiveRetainedCount > 100)
        {
            throw new ValidationException("Log archive retained count cannot exceed 100");
        }

        if (ArchiveTimeLimitHours > 1440) // 24 * 60
        {
            throw new ValidationException("Log archive time limit cannot exceed 60 days");
        }
        
        if (ArchiveRetainedCount is 0 && ArchiveTimeLimitHours is 0 && ArchiveEnabled)
        {
            throw new ValidationException("Archiving is enabled, but no retention policy is set. Please set either a retained file count or time limit");
        }
    }
}