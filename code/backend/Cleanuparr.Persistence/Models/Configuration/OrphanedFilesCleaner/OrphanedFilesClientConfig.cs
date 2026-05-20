using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.OrphanedFilesCleaner;

public sealed record OrphanedFilesClientConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DownloadClientConfigId { get; set; }

    public DownloadClientConfig DownloadClientConfig { get; set; } = null!;

    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Directories to scan for orphaned files on this download client.
    /// </summary>
    public List<string> ScanDirectories { get; set; } = [];

    /// <summary>
    /// Directory where orphaned files are moved.
    /// If null or empty, orphaned files are logged but not moved.
    /// </summary>
    public string? OrphanedDirectory { get; set; }

    /// <summary>
    /// Source path prefix reported by this download client (e.g. /downloads).
    /// Used for path remapping when paths differ between the container and the host.
    /// </summary>
    public string? DownloadDirectorySource { get; set; }

    /// <summary>
    /// Target path prefix on the local filesystem (e.g. /mnt/data).
    /// Used for path remapping when paths differ between the container and the host.
    /// </summary>
    public string? DownloadDirectoryTarget { get; set; }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (ScanDirectories.Count == 0)
        {
            throw new ValidationException("At least one scan directory is required when orphaned files cleaner is enabled for this client");
        }

        if (!string.IsNullOrEmpty(DownloadDirectorySource) != !string.IsNullOrEmpty(DownloadDirectoryTarget))
        {
            throw new ValidationException("Both download directory source and target must be set, or both must be empty");
        }
    }
}
