using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cleanuparr.Persistence.Models.Configuration.QueueCleaner;

public sealed record QueueCleanerConfig : IJobConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public bool Enabled { get; set; }
    
    public string CronExpression { get; set; } = "0 0/5 * * * ?";
    
    /// <summary>
    /// Indicates whether to use the CronExpression directly or convert from a user-friendly schedule
    /// </summary>
    public bool UseAdvancedScheduling { get; set; } = false;
    
    public FailedImportConfig FailedImport { get; set; } = new();
    
    public StalledConfig Stalled { get; set; } = new();
    
    public SlowConfig Slow { get; set; } = new();

    public List<string> IgnoredDownloads { get; set; } = [];
    
    public void Validate()
    {
        FailedImport.Validate();
        Stalled.Validate();
        Slow.Validate();
    }
}