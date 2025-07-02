using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.ContentBlocker;

public sealed record ContentBlockerConfig : IJobConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public bool Enabled { get; set; }
    
    public string CronExpression { get; set; } = "0/5 * * * * ?";
    
    public bool UseAdvancedScheduling { get; set; }

    public bool IgnorePrivate { get; set; }
    
    public bool DeletePrivate { get; set; }

    public BlocklistSettings Sonarr { get; set; } = new();
    
    public BlocklistSettings Radarr { get; set; } = new();
    
    public BlocklistSettings Lidarr { get; set; } = new();
    
    public BlocklistSettings Readarr { get; set; } = new();
    
    public BlocklistSettings Whisparr { get; set; } = new();
    
    public void Validate()
    {
        ValidateBlocklistSettings(Sonarr, "Sonarr");
        ValidateBlocklistSettings(Radarr, "Radarr");
        ValidateBlocklistSettings(Lidarr, "Lidarr");
        ValidateBlocklistSettings(Readarr, "Readarr");
        ValidateBlocklistSettings(Whisparr, "Whisparr");
    }
    
    private static void ValidateBlocklistSettings(BlocklistSettings settings, string context)
    {
        if (settings.Enabled && string.IsNullOrWhiteSpace(settings.BlocklistPath))
        {
            throw new ValidationException($"{context} blocklist is enabled but path is not specified");
        }
    }
}