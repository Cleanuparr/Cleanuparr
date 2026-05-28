namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public interface ISeedersFilterable
{
    /// <summary>
    /// Minimum number of seeders required before cleanup. Negative values disable the check.
    /// </summary>
    int MinSeeders { get; set; }
}