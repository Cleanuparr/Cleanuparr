namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Seeker search activity in the window (SearchTriggered events). A single event is tracked per search and
/// updated in place as the search completes, so the counts never double-count. Excludes dry-run activity
/// unless the caller opts in.
/// </summary>
public class SearchesV2Stats
{
    /// <summary>
    /// Total searches triggered in the window (regardless of their current status).
    /// </summary>
    public int Triggered { get; set; }

    /// <summary>
    /// Searches that completed successfully.
    /// </summary>
    public int Completed { get; set; }

    /// <summary>
    /// Searches that did not succeed — both failed and timed-out searches.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Total number of items grabbed as a result of the triggered searches.
    /// </summary>
    public int Grabbed { get; set; }

    /// <summary>
    /// Searches grouped by the reason they were triggered (Missing, QualityCutoffNotMet, ...).
    /// Keys are PascalCase search-reason names; only reasons with activity are present.
    /// </summary>
    public Dictionary<string, int> ByReason { get; set; } = new();
}
