using Cleanuparr.Infrastructure.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Controllers;

/// <summary>
/// Controller for checking the health of download clients
/// </summary>
[ApiController]
[Route("api/health")]
[Authorize]
public class HealthCheckController : ControllerBase
{
    private readonly ILogger<HealthCheckController> _logger;
    private readonly IHealthCheckService _healthCheckService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckController"/> class
    /// </summary>
    public HealthCheckController(
        ILogger<HealthCheckController> logger,
        IHealthCheckService healthCheckService)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Gets the health status of all download clients
    /// </summary>
    [HttpGet]
    public IActionResult GetAllHealth()
    {
        try
        {
            var healthStatuses = _healthCheckService.GetAllClientHealth();
            return Ok(healthStatuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client health statuses");
            return StatusCode(500, new { Error = "An error occurred while retrieving client health statuses" });
        }
    }

    /// <summary>
    /// Gets the health status of a specific download client
    /// </summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetClientHealth(Guid id)
    {
        try
        {
            var healthStatus = _healthCheckService.GetClientHealth(id);
            if (healthStatus == null)
            {
                return NotFound(new { Message = $"Health status for client with ID '{id}' not found" });
            }

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health status for client {id}", id);
            return StatusCode(500, new { Error = "An error occurred while retrieving the client health status" });
        }
    }

    /// <summary>
    /// Triggers a health check for all download clients
    /// </summary>
    [HttpPost("check")]
    public async Task<IActionResult> CheckAllHealth()
    {
        try
        {
            var results = await _healthCheckService.CheckAllClientsHealthAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health for all clients");
            return StatusCode(500, new { Error = "An error occurred while checking client health" });
        }
    }

    /// <summary>
    /// Triggers a health check for a specific download client
    /// </summary>
    [HttpPost("check/{id:guid}")]
    public async Task<IActionResult> CheckClientHealth(Guid id)
    {
        try
        {
            var result = await _healthCheckService.CheckClientHealthAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health for client {id}", id);
            return StatusCode(500, new { Error = "An error occurred while checking client health" });
        }
    }
}
