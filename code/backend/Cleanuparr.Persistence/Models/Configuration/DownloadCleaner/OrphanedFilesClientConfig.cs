using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public sealed record OrphanedFilesClientConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DownloadClientConfigId { get; set; }

    public DownloadClientConfig DownloadClientConfig { get; set; } = null!;

    public bool Enabled { get; set; }

    public List<string> ScanDirectories { get; set; } = [];

    /// <summary>
    /// If null or empty, orphaned files are logged but not moved.
    /// </summary>
    public string? OrphanedDirectory { get; set; }

    public List<string> ExcludePatterns { get; set; } = [];

    [Range(0, int.MaxValue)]
    public int MinFileAgeMinutes { get; set; }

    [Range(1, int.MaxValue)]
    public int? EmptyAfterXDays { get; set; }

    public void Validate() => Validate([]);

    public void Validate(IReadOnlyList<OrphanedFilesClientConfig> siblings)
    {
        if (!Enabled)
        {
            return;
        }

        if (ScanDirectories.Count == 0)
        {
            throw new ValidationException("At least one scan directory is required when orphaned files cleaner is enabled for this client");
        }

        if (siblings.Count == 0)
        {
            return;
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
        }

        if (!string.IsNullOrWhiteSpace(OrphanedDirectory))
        {
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
