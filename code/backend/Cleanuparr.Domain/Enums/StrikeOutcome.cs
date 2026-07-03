namespace Cleanuparr.Domain.Enums;

/// <summary>
/// Final outcome of a strike once it leaves the active enforcement table and is archived to history.
/// </summary>
public enum StrikeOutcome
{
    /// <summary>
    /// The download recovered (progress/speed/eta/seeders) and its strikes were reset.
    /// </summary>
    Recovered,

    /// <summary>
    /// The download hit the strike limit and was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The download went inactive without recovering or being removed, and its strikes expired.
    /// </summary>
    Expired,
}
