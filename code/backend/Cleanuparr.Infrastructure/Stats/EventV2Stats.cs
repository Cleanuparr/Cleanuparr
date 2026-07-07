namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Raw event audit for the window. Excludes dry-run events unless the caller opts in.
/// </summary>
public class EventV2Stats
{
    /// <summary>
    /// Total number of events in the window. Equal to the sum of <see cref="ByType"/>.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Events grouped by event type. Keys are PascalCase event-type names; only types with activity are present.
    /// </summary>
    public Dictionary<string, int> ByType { get; set; } = new();

    /// <summary>
    /// Events grouped by severity (Information, Warning, Important, Error). Only severities with activity are present.
    /// </summary>
    public Dictionary<string, int> BySeverity { get; set; } = new();
}
