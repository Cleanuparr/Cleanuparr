namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Strike activity within the requested timeframe. All counts are derived from strike events
/// (StalledStrike, FailedImportStrike, etc.) and exclude dry-run activity unless the caller opts in.
/// </summary>
public class StrikeV2Stats
{
    /// <summary>
    /// Total strikes issued in the timeframe. Equal to the sum of <see cref="ByType"/>.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Strikes issued in the timeframe, grouped by strike type (Stalled, FailedImport, SlowSpeed, ...).
    /// Keys are PascalCase strike-type names; only types with activity are present.
    /// </summary>
    public Dictionary<string, int> ByType { get; set; } = new();

    /// <summary>
    /// Number of downloads that recovered in the timeframe and had their strikes reset (StrikeReset events).
    /// </summary>
    public int Recovered { get; set; }
}
