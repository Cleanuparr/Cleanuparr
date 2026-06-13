using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public sealed record DeadTorrentConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DownloadClientConfigId { get; set; }

    public DownloadClientConfig DownloadClientConfig { get; set; } = null!;

    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Category/tag a dead torrent is moved to once it reaches <see cref="MaxStrikes"/>.
    /// </summary>
    public string TargetCategory { get; set; } = "cleanuparr-dead";

    /// <summary>
    /// When true, add a tag/label instead of changing the category. Supported by qBittorrent and Transmission.
    /// </summary>
    public bool UseTag { get; set; }

    /// <summary>
    /// Number of consecutive runs a torrent must report zero seeders before being moved.
    /// </summary>
    public int MaxStrikes { get; set; }

    /// <summary>
    /// Source categories to scan for dead torrents. At least one must be specified.
    /// </summary>
    public List<string> Categories { get; set; } = [];

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetCategory))
        {
            throw new ValidationException("Dead torrent target category is required");
        }

        if (Categories.Count is 0)
        {
            throw new ValidationException("No dead torrent categories configured");
        }

        if (Categories.Contains(TargetCategory, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("The dead torrent target category should not be present in dead torrent categories");
        }

        if (Categories.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException("Empty dead torrent category filter found");
        }

        if (MaxStrikes < 3)
        {
            throw new ValidationException("Dead torrent max strikes must be at least 3");
        }
    }
}
