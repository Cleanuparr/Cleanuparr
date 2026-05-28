namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public interface ISeedersFilterable
{
    /// <summary>
    /// Minimum number of seeders required before cleanup. Set to 0 to disable.
    /// </summary>
    int MinSeeders { get; set; }
}
