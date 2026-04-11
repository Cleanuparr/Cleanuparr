using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public sealed record TransmissionSeedingRule : ISeedingRule, ITagFilterable
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DownloadClientConfigId { get; set; }

    public DownloadClientConfig DownloadClientConfig { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public List<string> Categories { get; set; } = [];

    public List<string> TrackerPatterns { get; set; } = [];

    /// <summary>
    /// The torrent must have at least one of these Transmission labels. Empty = no label filter.
    /// </summary>
    public List<string> TagsAny { get; set; } = [];

    /// <summary>
    /// The torrent must have ALL of these Transmission labels. Empty = no label filter.
    /// </summary>
    public List<string> TagsAll { get; set; } = [];

    public int Priority { get; set; }

    /// <summary>
    /// Which torrent privacy types this rule applies to.
    /// </summary>
    public TorrentPrivacyType PrivacyType { get; set; } = TorrentPrivacyType.Public;

    /// <summary>
    /// Max ratio before removing a download.
    /// </summary>
    public double MaxRatio { get; set; } = -1;

    /// <summary>
    /// Min number of hours to seed before removing a download, if the ratio has been met.
    /// </summary>
    public double MinSeedTime { get; set; }

    /// <summary>
    /// Number of hours to seed before removing a download.
    /// </summary>
    public double MaxSeedTime { get; set; } = -1;

    /// <summary>
    /// Whether to delete the source files when cleaning the download.
    /// </summary>
    public bool DeleteSourceFiles { get; set; }

    public void Validate()
    {
        if (string.IsNullOrEmpty(Name.Trim()))
        {
            throw new ValidationException("Rule name can not be empty");
        }

        if (Categories.Count == 0)
        {
            throw new ValidationException("At least one category must be specified");
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
