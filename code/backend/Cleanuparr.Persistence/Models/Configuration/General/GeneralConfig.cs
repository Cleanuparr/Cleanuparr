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

    public bool SearchEnabled { get; set; } = true;
    
    public ushort SearchDelay { get; set; } = Constants.DefaultSearchDelaySeconds;

    public string EncryptionKey { get; set; } = Guid.NewGuid().ToString();

    public List<string> IgnoredDownloads { get; set; } = [];

    public LoggingConfig Log { get; set; } = new();

    public void Validate()
    {
        if (HttpTimeout is 0)
        {
            throw new ValidationException($"{nameof(HttpTimeout)} must be greater than 0");
        }

        if (SearchDelay < Constants.MinSearchDelaySeconds)
        {
            throw new ValidationException($"{nameof(SearchDelay)} must be at least {Constants.MinSearchDelaySeconds} seconds");
        }

        Log.Validate();
    }
}