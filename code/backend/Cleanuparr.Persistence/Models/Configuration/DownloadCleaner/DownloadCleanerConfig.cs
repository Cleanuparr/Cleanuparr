using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public sealed record DownloadCleanerConfig : IJobConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Enabled { get; set; }

    public string CronExpression { get; set; } = "0 0 * * * ?";

    /// <summary>
    /// Indicates whether to use the CronExpression directly or convert from a user-friendly schedule
    /// </summary>
    public bool UseAdvancedScheduling { get; set; }

    public List<string> IgnoredDownloads { get; set; } = [];

    public void Validate()
    {
    }
}
