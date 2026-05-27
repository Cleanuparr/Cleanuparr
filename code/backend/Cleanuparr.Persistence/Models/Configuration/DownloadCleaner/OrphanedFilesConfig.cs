using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

/// <summary>
/// Per-download-client configuration for the orphaned files scanner.
/// </summary>
public sealed record OrphanedFilesConfig : IConfig
{
    /// <summary>
    /// Unique identifier for this config row.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Owning download client identifier.
    /// </summary>
    public Guid DownloadClientConfigId { get; set; }

    /// <summary>
    /// Navigation back to the owning download client.
    /// </summary>
    public DownloadClientConfig DownloadClientConfig { get; set; } = null!;

    /// <summary>
    /// Whether the orphaned files scanner is enabled for this client.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Absolute paths to scan for orphaned files. Each top-level entry is
    /// checked against the client's active torrents.
    /// </summary>
    public List<string> ScanDirectories { get; set; } = [];

    /// <summary>
    /// Destination directory where orphaned entries are moved.
    /// </summary>
    [Required]
    public string OrphanedDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Glob patterns that exclude entries from being treated as orphaned
    /// (e.g. "*.nfo", ".DS_Store").
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = [];

    /// <summary>
    /// Minimum age in hours an entry must have before it can be considered
    /// orphaned. Protects in-flight downloads that the client has not yet
    /// registered as a torrent.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MinFileAgeHours { get; set; } = 24;

    /// <summary>
    /// If set, entries in <see cref="OrphanedDirectory"/> older than this many
    /// days are permanently deleted. Null leaves them indefinitely.
    /// </summary>
    ///  TODO change to int hours, min 1
    [Range(1, int.MaxValue)]
    public int? EmptyAfterXDays { get; set; }

    /// <summary>
    /// Self-validation with no cross-client checks.
    /// </summary>
    public void Validate() => Validate([], []);

    /// <summary>
    /// Validates this config and ensures its scan/orphaned paths do not
    /// overlap with any sibling client's orphaned-files config or another
    /// client's download directory target.
    /// </summary>
    public void Validate(
        IReadOnlyList<OrphanedFilesConfig> siblings,
        IReadOnlyList<DownloadClientConfig>? otherDownloadClients = null)
    {
        otherDownloadClients ??= [];

        if (!Enabled)
        {
            return;
        }

        if (ScanDirectories.Count == 0)
        {
            throw new ValidationException("At least one scan directory is required when orphaned files cleanup is enabled for this client");
        }

        if (string.IsNullOrWhiteSpace(OrphanedDirectory))
        {
            throw new ValidationException("Orphaned directory is required when orphaned files cleanup is enabled for this client");
        }

        foreach (var scanDir in ScanDirectories)
        {
            var normalized = NormalizePath(scanDir);

            foreach (var sibling in siblings)
            {
                foreach (var otherScanDir in sibling.ScanDirectories)
                {
                    CheckOverlap(normalized, NormalizePath(otherScanDir), "scan directory", "another client's scan directory");
                }

                if (!string.IsNullOrWhiteSpace(sibling.OrphanedDirectory))
                {
                    CheckOverlap(normalized, NormalizePath(sibling.OrphanedDirectory), "scan directory", "another client's orphaned directory");
                }
            }

            foreach (var otherClient in otherDownloadClients)
            {
                if (!string.IsNullOrWhiteSpace(otherClient.DownloadDirectoryTarget))
                {
                    CheckOverlap(normalized, NormalizePath(otherClient.DownloadDirectoryTarget), "scan directory", $"another client's download directory ({otherClient.Name})");
                }
            }
        }

        var normalizedOrphaned = NormalizePath(OrphanedDirectory);

        foreach (var sibling in siblings)
        {
            foreach (var otherScanDir in sibling.ScanDirectories)
            {
                CheckOverlap(normalizedOrphaned, NormalizePath(otherScanDir), "orphaned directory", "another client's scan directory");
            }

            if (!string.IsNullOrWhiteSpace(sibling.OrphanedDirectory))
            {
                CheckOverlap(normalizedOrphaned, NormalizePath(sibling.OrphanedDirectory), "orphaned directory", "another client's orphaned directory");
            }
        }

        foreach (var otherClient in otherDownloadClients)
        {
            if (!string.IsNullOrWhiteSpace(otherClient.DownloadDirectoryTarget))
            {
                CheckOverlap(normalizedOrphaned, NormalizePath(otherClient.DownloadDirectoryTarget), "orphaned directory", $"another client's download directory ({otherClient.Name})");
            }
        }
    }

    private static void CheckOverlap(string a, string b, string aLabel, string bLabel)
    {
        var sep = Path.DirectorySeparatorChar.ToString();

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + sep, StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a + sep, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"Path overlap detected: {aLabel} '{a}' overlaps with {bLabel} '{b}'. Scan directories and orphaned directories must not overlap across clients.");
        }
    }

    private static string NormalizePath(string path) =>
        string.Join(Path.DirectorySeparatorChar, path.Split(['\\', '/']))
            .TrimEnd(Path.DirectorySeparatorChar);
}
