using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Domain.Enums;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

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

    public List<SeedingRule> Categories { get; set; } = [];

    /// <summary>
    /// Indicates whether unlinked download handling is enabled
    /// </summary>
    public bool UnlinkedEnabled { get; set; } = false;
    
    public string UnlinkedTargetCategory { get; set; } = "cleanuparr-unlinked";

    public bool UnlinkedUseTag { get; set; }

    public List<string> UnlinkedIgnoredRootDirs { get; set; } = [];
    
    public List<string> UnlinkedCategories { get; set; } = [];

    public List<string> IgnoredDownloads { get; set; } = [];

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        // Validate that at least one feature is configured
        bool hasSeedingCategories = Categories.Count > 0;
        bool hasUnlinkedFeature = UnlinkedEnabled && UnlinkedCategories.Count > 0 && !string.IsNullOrWhiteSpace(UnlinkedTargetCategory);

        if (!hasSeedingCategories && !hasUnlinkedFeature)
        {
            throw new ValidationException("No features are enabled");
        }

        if (Categories.GroupBy(x => new { Name = x.Name.ToUpperInvariant(), x.PrivacyType }).Any(x => x.Count() > 1))
        {
            throw new ValidationException("Duplicated clean category and privacy type combination found");
        }

        var categoriesByName = Categories.GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase);
        foreach (var group in categoriesByName)
        {
            if (group.Count() > 1 && group.Any(x => x.PrivacyType == TorrentPrivacyType.Both))
            {
                throw new ValidationException(
                    $"Category '{group.Key}' has a rule with privacy type 'Both' which already covers all torrent types");
            }
        }
        
        Categories.ForEach(x => x.Validate());
        
        // Only validate unlinked settings if unlinked handling is enabled
        if (!UnlinkedEnabled)
        {
            return;
        }
        
        if (string.IsNullOrEmpty(UnlinkedTargetCategory))
        {
            throw new ValidationException("unlinked target category is required");
        }

        if (UnlinkedCategories.Count is 0)
        {
            throw new ValidationException("No unlinked categories configured");
        }

        if (UnlinkedCategories.Contains(UnlinkedTargetCategory))
        {
            throw new ValidationException("The unlinked target category should not be present in unlinked categories");
        }

        if (UnlinkedCategories.Any(string.IsNullOrEmpty))
        {
            throw new ValidationException("Empty unlinked category filter found");
        }

        foreach (var dir in UnlinkedIgnoredRootDirs.Where(d => !string.IsNullOrEmpty(d)))
        {
            if (!Directory.Exists(dir))
            {
                throw new ValidationException($"{dir} root directory does not exist");
            }
        }
    }
}