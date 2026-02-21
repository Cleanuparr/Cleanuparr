using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public sealed record SeedingRule : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public Guid DownloadCleanerConfigId { get; set; }
    
    public DownloadCleanerConfig DownloadCleanerConfig { get; set; }
    
    public required string Name { get; init; }

    /// <summary>
    /// Which torrent privacy types this rule applies to.
    /// </summary>
    public TorrentPrivacyType PrivacyType { get; init; } = TorrentPrivacyType.Public;

    /// <summary>
    /// Max ratio before removing a download.
    /// </summary>
    public required double MaxRatio { get; init; } = -1;

    /// <summary>
    /// Min number of hours to seed before removing a download, if the ratio has been met.
    /// </summary>
    public required double MinSeedTime { get; init; }

    /// <summary>
    /// Number of hours to seed before removing a download.
    /// </summary>
    public required double MaxSeedTime { get; init; } = -1;

    /// <summary>
    /// Whether to delete the source files when cleaning the download.
    /// </summary>
    public required bool DeleteSourceFiles { get; init; }

    public void Validate()
    {
        if (string.IsNullOrEmpty(Name.Trim()))
        {
            throw new ValidationException("Category name can not be empty");
        }

        if (MaxRatio < 0 && MaxSeedTime < 0)
        {
            throw new ValidationException("Either max ratio or max seed time must be set to a non-negative value");
        }

        if (MinSeedTime < 0)
        {
            throw new ValidationException("Min seed time can not be negative");
        }
    }
}