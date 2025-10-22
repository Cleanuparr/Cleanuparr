using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Entities;

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
    
    public List<string> IgnoredDownloads { get; set; } = [];
    
    public ushort DownloadingMetadataMaxStrikes { get; set; }
    
    public List<StallRule> StallRules { get; set; } = [];
    
    public List<SlowRule> SlowRules { get; set; } = [];
    
    public void Validate()
    {
        FailedImport.Validate();
        
        if (DownloadingMetadataMaxStrikes is > 0 and < 3)
        {
            throw new ValidationException("the minimum value for downloading metadata max strikes must be 3");
        }

        foreach (var rule in StallRules)
        {
            rule.Validate();
        }

        foreach (var rule in SlowRules)
        {
            rule.Validate();
        }
            
        // Check for duplicate names within each rule type
        var stallNames = StallRules.Where(r => r.Enabled).Select(r => r.Name).ToList();
        if (stallNames.Count != stallNames.Distinct().Count())
        {
            throw new Cleanuparr.Domain.Exceptions.ValidationException("Duplicate stall rule names found");
        }
            
        var slowNames = SlowRules.Where(r => r.Enabled).Select(r => r.Name).ToList();
        if (slowNames.Count != slowNames.Distinct().Count())
        {
            throw new Cleanuparr.Domain.Exceptions.ValidationException("Duplicate slow rule names found");
        }
    }
}