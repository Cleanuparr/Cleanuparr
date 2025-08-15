using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Serilog;
using Serilog.Events;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.General;

public sealed record GeneralConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public bool DisplaySupportBanner { get; set; } = true;
    
    public bool DryRun { get; set; }
    
    public ushort HttpMaxRetries { get; set; }
    
    public ushort HttpTimeout { get; set; } = 100;
    
    public CertificateValidationType HttpCertificateValidation { get; set; } = CertificateValidationType.Enabled;

    public bool SearchEnabled { get; set; } = true;
    
    public ushort SearchDelay { get; set; } = 30;
    
    public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;

    // Rolling Configuration
    public RollingInterval LogRollingInterval { get; set; } = RollingInterval.Day;
    public ushort LogRollingSizeMB { get; set; } = 10; // 0 = disabled
    public ushort LogRetainedFileCount { get; set; } = 5; // 0 = unlimited

    // Archive Configuration  
    public bool LogArchiveEnabled { get; set; } = true;
    public ushort LogArchiveRetainedCount { get; set; } = 30; // 0 = unlimited
    public ushort LogArchiveTimeLimitDays { get; set; } = 30; // 0 = unlimited

    public string EncryptionKey { get; set; } = Guid.NewGuid().ToString();

    public List<string> IgnoredDownloads { get; set; } = [];

    public void Validate()
    {
        if (HttpTimeout is 0)
        {
            throw new ValidationException("HTTP_TIMEOUT must be greater than 0");
        }

        // Validate rolling interval is only Hour or Day
        if (LogRollingInterval != RollingInterval.Hour && LogRollingInterval != RollingInterval.Day)
        {
            throw new ValidationException("Log rolling interval must be either Hour or Day");
        }

        // Validate rolling file size
        if (LogRollingSizeMB > 100)
        {
            throw new ValidationException("Log rolling size cannot exceed 100 MB");
        }

        // Validate retained file count
        if (LogRetainedFileCount > 50)
        {
            throw new ValidationException("Log retained file count cannot exceed 50");
        }

        // Validate archive retained count
        if (LogArchiveRetainedCount > 100)
        {
            throw new ValidationException("Log archive retained count cannot exceed 100");
        }

        // Validate archive time limit
        if (LogArchiveTimeLimitDays > 60)
        {
            throw new ValidationException("Log archive time limit cannot exceed 60 days");
        }
    }
}