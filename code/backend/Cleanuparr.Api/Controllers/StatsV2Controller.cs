using Cleanuparr.Infrastructure.Stats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Controllers;

/// <summary>
/// Comprehensive v2 statistics for the dashboard, derived from the event stream (active + archived history).
/// v1 (<see cref="StatsController"/>) is left untouched.
/// </summary>
[ApiController]
[Route("api/v2/stats")]
[Authorize]
public class StatsV2Controller : ControllerBase
{
    private readonly IStatsService _statsService;

    public StatsV2Controller(IStatsService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>
    /// Aggregated statistics for the given timeframe.
    /// </summary>
    /// <param name="hours">Timeframe in hours (default 168, range 1-8760)</param>
    [HttpGet]
    public async Task<IActionResult> GetStats([FromQuery] int hours = 168)
    {
        hours = Math.Clamp(hours, 1, 8760);
        StatsV2Response stats = await _statsService.GetStatsV2Async(hours);
        return Ok(stats);
    }

    /// <summary>
    /// Day-bucketed timeline for a single metric.
    /// </summary>
    /// <param name="metric">strikesIssued | recovered | removed | malwareBlocked | events</param>
    /// <param name="hours">Timeframe in hours (default 720, range 1-8760)</param>
    /// <param name="bucket">Only "day" is currently supported.</param>
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] string metric = "events",
        [FromQuery] int hours = 720,
        [FromQuery] string bucket = "day")
    {
        if (!string.Equals(bucket, "day", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest($"Unsupported bucket '{bucket}'. Only 'day' is currently supported.");
        }

        hours = Math.Clamp(hours, 1, 8760);
        List<TimelineBucketDto> series = await _statsService.GetTimelineAsync(metric, hours);
        return Ok(series);
    }
}
