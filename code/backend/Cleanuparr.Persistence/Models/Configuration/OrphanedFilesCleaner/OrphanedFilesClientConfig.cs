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

    }
}
