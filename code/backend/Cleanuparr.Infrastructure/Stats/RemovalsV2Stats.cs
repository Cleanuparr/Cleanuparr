namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Downloads removed from the queue in the timeframe (QueueItemDeleted events), broken down by reason.
/// This is the single source of truth for removals: the "malware blocked" figure is simply the sum of
/// the AllFilesBlocked and AtLeastOneFileBlocked reasons. Excludes dry-run activity unless the caller opts in.
/// </summary>
public class RemovalsV2Stats
{
    /// <summary>
    /// Total downloads removed in the timeframe (all reasons). Equal to the sum of <see cref="ByReason"/>.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Removals grouped by delete reason (Stalled, FailedImport, AllFilesBlocked, ...).
    /// Keys are PascalCase delete-reason names; only reasons with activity are present.
    /// </summary>
    public Dictionary<string, int> ByReason { get; set; } = new();
}
