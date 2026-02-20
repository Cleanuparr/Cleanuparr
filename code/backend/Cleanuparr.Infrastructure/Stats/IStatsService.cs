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
    /// <returns>Aggregated stats response</returns>
    Task<StatsResponse> GetStatsAsync(int hours = 24);
}
