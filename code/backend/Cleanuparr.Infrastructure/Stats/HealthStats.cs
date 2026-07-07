namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Current health of configured integrations. This is a cached gauge refreshed by a background service
/// (roughly every 5 minutes), not a windowed metric — it ignores the requested timeframe.
/// </summary>
public class HealthStats
{
    /// <summary>
    /// Health of each enabled download client.
    /// </summary>
    public List<DownloadClientHealthDto> DownloadClients { get; set; } = [];

    /// <summary>
    /// Health of each enabled arr instance (Sonarr, Radarr, Lidarr, Readarr, Whisparr).
    /// </summary>
    public List<ArrInstanceHealthDto> ArrInstances { get; set; } = [];
}
