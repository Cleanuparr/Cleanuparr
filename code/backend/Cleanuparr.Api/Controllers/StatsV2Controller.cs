using Cleanuparr.Api.Common;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Stats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Controllers;

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
    /// Aggregated statistics for the given timeframe. Every section except health is scoped to the timeframe and, by
    /// default, excludes dry-run activity.
    /// </summary>
    /// <param name="hours">Timeframe in hours (default 168, range 1-8760)</param>
    /// <param name="includeDryRun">Include dry-run activity in the timeframe-scoped sections (default false)</param>
    [HttpGet]
    public async Task<IActionResult> GetStats([FromQuery] int hours = 168, [FromQuery] bool includeDryRun = false)
    {
        hours = TimelineWindow.ClampHours(hours);
        StatsV2Response stats = await _statsService.GetStatsV2Async(hours, includeDryRun);
        return Ok(stats);
    }

    /// <summary>
    /// Bucketed timeline for a single metric.
    /// </summary>
    /// <param name="metric">strikesIssued | recovered | removed | malwareBlocked | events</param>
    /// <param name="hours">Timeframe in hours (default 720, range 1-8760)</param>
    /// <param name="bucket">Bucket size: hour | day | week | month. When omitted, hourly for timeframes up to 24h, daily otherwise.</param>
    /// <param name="includeDryRun">Include dry-run activity (default false)</param>
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] string metric = "events",
        [FromQuery] int hours = 720,
        [FromQuery] string? bucket = null,
        [FromQuery] bool includeDryRun = false)
    {
        TimelineBucketSize? size = null;
        if (!string.IsNullOrWhiteSpace(bucket))
        {
            if (!Enum.TryParse(bucket, ignoreCase: true, out TimelineBucketSize parsed) || !Enum.IsDefined(parsed))
            {
                return BadRequest($"Unsupported bucket '{bucket}'. Supported values: hour, day, week, month.");
            }

            size = parsed;
        }

        hours = TimelineWindow.ClampHours(hours);
        List<TimelineBucketDto> series = await _statsService.GetTimelineAsync(metric, hours, size, includeDryRun);
        return Ok(series);
    }
}
