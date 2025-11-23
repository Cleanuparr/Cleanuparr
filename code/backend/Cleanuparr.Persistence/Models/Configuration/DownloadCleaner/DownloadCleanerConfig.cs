using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    public List<CleanCategory> Categories { get; set; } = [];

    public bool DeletePrivate { get; set; }
    
    /// <summary>
    /// Indicates whether unlinked download handling is enabled
    /// </summary>
    public bool UnlinkedEnabled { get; set; } = false;
    
    public string UnlinkedTargetCategory { get; set; } = "cleanuparr-unlinked";

    public bool UnlinkedUseTag { get; set; }

    public string UnlinkedIgnoredRootDir { get; set; } = string.Empty;
    
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

        if (Categories.GroupBy(x => x.Name).Any(x => x.Count() > 1))
        {
            throw new ValidationException("Duplicated clean categories found");
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

        if (!string.IsNullOrEmpty(UnlinkedIgnoredRootDir) && !Directory.Exists(UnlinkedIgnoredRootDir))
        {
            throw new ValidationException($"{UnlinkedIgnoredRootDir} root directory does not exist");
        }
    }
}