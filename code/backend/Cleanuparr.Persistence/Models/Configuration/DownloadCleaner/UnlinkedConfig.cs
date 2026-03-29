using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public sealed record UnlinkedConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DownloadClientConfigId { get; set; }

    public DownloadClientConfig DownloadClientConfig { get; set; } = null!;

    public bool Enabled { get; set; } = false;

    public string TargetCategory { get; set; } = "cleanuparr-unlinked";

    public bool UseTag { get; set; }

    public List<string> IgnoredRootDirs { get; set; } = [];

    public List<string> Categories { get; set; } = [];

    /// <summary>
    /// The path prefix reported by the download client (e.g., "/downloads").
    /// When set, this prefix is replaced with <see cref="DownloadDirectoryTarget"/> when resolving file paths.
    /// </summary>
    public string? DownloadDirectorySource { get; set; }

    /// <summary>
    /// The actual local mount path (e.g., "/downloads-deluge").
    /// Replaces <see cref="DownloadDirectorySource"/> in file paths for hardlink checking.
    /// </summary>
    public string? DownloadDirectoryTarget { get; set; }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(TargetCategory))
        {
            throw new ValidationException("Unlinked target category is required");
        }

        if (Categories.Count is 0)
        {
            throw new ValidationException("No unlinked categories configured");
        }

        if (Categories.Contains(TargetCategory))
        {
            throw new ValidationException("The unlinked target category should not be present in unlinked categories");
        }

        if (Categories.Any(string.IsNullOrEmpty))
        {
            throw new ValidationException("Empty unlinked category filter found");
        }

        if (!string.IsNullOrEmpty(DownloadDirectorySource) != !string.IsNullOrEmpty(DownloadDirectoryTarget))
        {
            throw new ValidationException("Both download directory source and target must be set, or both must be empty");
        }

        foreach (var dir in IgnoredRootDirs.Where(d => !string.IsNullOrEmpty(d)))
        {
            if (!Directory.Exists(dir))
            {
                throw new ValidationException($"{dir} root directory does not exist");
            }
        }
    }
}
