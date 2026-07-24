using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Shared.Helpers;
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


    public bool StatusCheckEnabled { get; set; } = true;

    public string EncryptionKey { get; set; } = Guid.NewGuid().ToString();

    public List<string> IgnoredDownloads { get; set; } = [];

    public bool ConnectivityCheckEnabled { get; set; }

    public List<string> ConnectivityCheckUrls { get; set; } = [];

    public ushort StrikeInactivityWindowHours { get; set; } = 24;

    /// <summary>
    /// How long archived strike/event history is retained before being pruned, in days.
    /// </summary>
    public ushort HistoryRetentionDays { get; set; } = 365;

    public LoggingConfig Log { get; set; } = new();

    public AuthConfig Auth { get; set; } = new();

    public void Validate()
    {
        if (HttpTimeout is 0)
        {
            throw new ValidationException("HTTP_TIMEOUT must be greater than 0");
        }

        if (StrikeInactivityWindowHours is 0)
        {
            throw new ValidationException("STRIKE_INACTIVITY_WINDOW_HOURS must be greater than 0");
        }

        if (StrikeInactivityWindowHours > 168)
        {
            throw new ValidationException("STRIKE_INACTIVITY_WINDOW_HOURS must be less than or equal to 168");
        }

        if (HistoryRetentionDays is 0)
        {
            throw new ValidationException("HISTORY_RETENTION_DAYS must be greater than 0");
        }

        if (HistoryRetentionDays > 3650)
        {
            throw new ValidationException("HISTORY_RETENTION_DAYS must be less than or equal to 3650");
        }

        if (ConnectivityCheckEnabled && ConnectivityCheckUrls.Count is 0)
        {
            throw new ValidationException("At least one connectivity check URL is required when the internet connectivity check is enabled");
        }

        Log.Validate();
        Auth.Validate();
    }
}