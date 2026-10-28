namespace Cleanuparr.Domain.Enums;

public enum RadarrReleaseGate
{
    /// <summary>
    /// Searches a movie once Radarr marks it released and its digital, physical or cinema date is past the grace period.
    /// </summary>
    ReleaseDate,

    /// <summary>
    /// Searches a movie once Radarr counts it as available, using its minimum availability and availability delay.
    /// The grace period does not apply.
    /// </summary>
    ArrAvailability,
}
