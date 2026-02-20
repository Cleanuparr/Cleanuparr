using Cleanuparr.Infrastructure.Stats;
using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Controllers;

/// <summary>
/// Aggregated statistics endpoint for dashboard integrations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly ILogger<StatsController> _logger;
    private readonly IStatsService _statsService;

    public StatsController(
        ILogger<StatsController> logger,
        IStatsService statsService)
    {
        _logger = logger;
        _statsService = statsService;
    }

    /// <summary>
    /// Gets aggregated application statistics for the specified timeframe
    /// </summary>
    /// <param name="hours">Timeframe in hours (default 24, range 1-720)</param>
    [HttpGet]
    public async Task<IActionResult> GetStats([FromQuery] int hours = 24)
    {
        try
        {
            hours = Math.Clamp(hours, 1, 720);

            var stats = await _statsService.GetStatsAsync(hours);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stats");
            return StatusCode(500, new { Error = "An error occurred while retrieving stats" });
        }
    }
}
