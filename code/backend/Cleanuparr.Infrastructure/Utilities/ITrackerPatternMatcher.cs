namespace Cleanuparr.Infrastructure.Utilities;

/// <summary>
/// Interface for tracker pattern matching functionality.
/// </summary>
public interface ITrackerPatternMatcher
{
    /// <summary>
    /// Checks if any tracker host matches any of the provided patterns.
    /// </summary>
    bool MatchesAny(IReadOnlyList<string> trackerHosts, IReadOnlyList<string> patterns);

    /// <summary>
    /// Checks if a tracker host matches a specific pattern.
    /// </summary>
    bool Matches(string trackerHost, string pattern);
}
