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
    /// Aggregated statistics for the given window (24h | 7d | 30d | 1y).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStats([FromQuery] string window = "7d")
    {
        int hours = WindowToHours(window);
        StatsV2Response stats = await _statsService.GetStatsV2Async(hours, window);
        return Ok(stats);
    }

    /// <summary>
    /// Day-bucketed timeline for a single metric.
    /// </summary>
    /// <param name="metric">strikesIssued | recovered | removed | malwareBlocked | events</param>
    /// <param name="window">24h | 7d | 30d | 1y</param>
    /// <param name="bucket">Only "day" is currently supported.</param>
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] string metric = "events",
        [FromQuery] string window = "30d",
        [FromQuery] string bucket = "day")
    {
        int hours = WindowToHours(window);
        List<TimelineBucketDto> series = await _statsService.GetTimelineAsync(metric, hours);
        return Ok(series);
    }

    private static int WindowToHours(string window) => window switch
    {
        "24h" => 24,
        "7d" => 24 * 7,
        "30d" => 24 * 30,
        "1y" => 24 * 365,
        _ => 24 * 7,
    };
}
