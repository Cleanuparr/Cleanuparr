namespace Cleanuparr.Infrastructure.Stats;

/// <summary>
/// Service for aggregating application statistics
/// </summary>
public interface IStatsService
{
    /// <summary>
    /// Gets aggregated statistics for the given timeframe
    /// </summary>
    /// <param name="hours">Timeframe in hours (default 24)</param>
    /// <param name="includeEvents">Number of recent events to include (0 = none)</param>
    /// <param name="includeStrikes">Number of recent strikes to include (0 = none)</param>
    /// <returns>Aggregated stats response</returns>
    Task<StatsResponse> GetStatsAsync(int hours = 24, int includeEvents = 0, int includeStrikes = 0);

    /// <param name="hours">Timeframe in hours</param>
    Task<StatsV2Response> GetStatsV2Async(int hours);

    /// <summary>
    /// Gets a day-bucketed timeline for a single metric over the given window.
    /// </summary>
    /// <param name="metric">strikesIssued | recovered | removed | malwareBlocked | events</param>
    Task<List<TimelineBucketDto>> GetTimelineAsync(string metric, int hours);
}
