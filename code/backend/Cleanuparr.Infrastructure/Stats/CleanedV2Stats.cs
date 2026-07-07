namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Downloads cleaned by the download cleaner in the window (DownloadCleaned events), broken down by reason.
/// Cleaning is distinct from a removal: it happens when a download meets its seeding goals, not because it
/// was struck out. Excludes dry-run activity unless the caller opts in.
/// </summary>
public class CleanedV2Stats
{
    /// <summary>
    /// Total downloads cleaned in the window (all reasons). Equal to the sum of <see cref="ByReason"/>.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Cleaned downloads grouped by clean reason (MaxRatioReached, MaxSeedTimeReached).
    /// Keys are PascalCase clean-reason names; only reasons with activity are present.
    /// </summary>
    public Dictionary<string, int> ByReason { get; set; } = new();
}
