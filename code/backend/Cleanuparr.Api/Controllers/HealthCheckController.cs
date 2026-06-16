using Cleanuparr.Api.Extensions;
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
    private readonly IHealthCheckService _healthCheckService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckController"/> class
    /// </summary>
    public HealthCheckController(IHealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Gets the health status of all download clients
    /// </summary>
    [HttpGet]
    public IActionResult GetAllHealth()
    {
        var healthStatuses = _healthCheckService.GetAllClientHealth();
        return Ok(healthStatuses);
    }

    /// <summary>
    /// Gets the health status of a specific download client
    /// </summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetClientHealth(Guid id)
    {
        var healthStatus = _healthCheckService.GetClientHealth(id);
        if (healthStatus == null)
        {
            return this.ProblemResult(StatusCodes.Status404NotFound, $"Health status for client with ID '{id}' not found");
        }

        return Ok(healthStatus);
    }

    /// <summary>
    /// Triggers a health check for all download clients
    /// </summary>
    [HttpPost("check")]
    public async Task<IActionResult> CheckAllHealth()
    {
        var results = await _healthCheckService.CheckAllClientsHealthAsync();
        return Ok(results);
    }

    /// <summary>
    /// Triggers a health check for a specific download client
    /// </summary>
    [HttpPost("check/{id:guid}")]
    public async Task<IActionResult> CheckClientHealth(Guid id)
    {
        var result = await _healthCheckService.CheckClientHealthAsync(id);
        return Ok(result);
    }
}
