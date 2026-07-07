using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Service for aggregating application statistics
/// </summary>
public interface IStatsService
{
    /// <summary>
    /// Gets aggregated statistics for the given timeframe (v1, deprecated — prefer <see cref="GetStatsV2Async"/>)
    /// </summary>
    /// <param name="hours">Timeframe in hours (default 24)</param>
    /// <param name="includeEvents">Number of recent events to include (0 = none)</param>
    /// <param name="includeStrikes">Number of recent strikes to include (0 = none)</param>
    /// <returns>Aggregated stats response</returns>
    Task<StatsResponse> GetStatsAsync(int hours = 24, int includeEvents = 0, int includeStrikes = 0);

    /// <summary>
    /// Gets aggregated statistics for the given window. Every section except health is windowed and,
    /// by default, excludes dry-run activity.
    /// </summary>
    /// <param name="hours">Timeframe in hours</param>
    /// <param name="includeDryRun">When true, dry-run events are included in the windowed sections</param>
    Task<StatsV2Response> GetStatsV2Async(int hours, bool includeDryRun = false);

    /// <summary>
    /// Gets a bucketed timeline for a single metric over the given window.
    /// </summary>
    /// <param name="metric">strikesIssued | recovered | removed | malwareBlocked | events</param>
    /// <param name="hours">Timeframe in hours</param>
    /// <param name="bucket">Bucket size; when null, defaults to hourly for windows up to 24h, daily otherwise</param>
    /// <param name="includeDryRun">When true, dry-run events are included</param>
    Task<List<TimelineBucketDto>> GetTimelineAsync(string metric, int hours, TimelineBucketSize? bucket = null, bool includeDryRun = false);
}
