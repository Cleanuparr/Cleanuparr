using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cleanuparr.Api.Controllers;

/// <summary>
/// Health check endpoints for Docker and Kubernetes
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(HealthCheckService healthCheckService, ILogger<HealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Basic liveness probe - checks if the application is running
    /// Used by Docker HEALTHCHECK and Kubernetes liveness probes
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [Route("/health")]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var result = await _healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains("liveness"));
            
            return result.Status == HealthStatus.Healthy 
                ? Ok(new { status = "healthy", timestamp = DateTime.UtcNow })
                : StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new { status = "unhealthy", error = "Health check failed", timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    /// Readiness probe - checks if the application is ready to serve traffic
    /// Used by Kubernetes readiness probes
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [Route("/health/ready")]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            var result = await _healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains("readiness"));
            
            if (result.Status == HealthStatus.Healthy)
            {
                return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
            }
            
            // For readiness, we consider degraded as not ready
            return StatusCode(503, new { 
                status = "not_ready", 
                timestamp = DateTime.UtcNow,
                details = result.Entries.Where(e => e.Value.Status != HealthStatus.Healthy)
                    .ToDictionary(e => e.Key, e => new { 
                        status = e.Value.Status.ToString().ToLowerInvariant(),
                        description = e.Value.Description 
                    })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new { status = "not_ready", error = "Readiness check failed", timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    /// Detailed health status - for monitoring and debugging
    /// </summary>
    [HttpGet]
    [Route("/health/detailed")]
    public async Task<IActionResult> GetDetailedHealth()
    {
        try
        {
            var result = await _healthCheckService.CheckHealthAsync();
            
            var response = new
            {
                status = result.Status.ToString().ToLowerInvariant(),
                timestamp = DateTime.UtcNow,
                totalDuration = result.TotalDuration.TotalMilliseconds,
                entries = result.Entries.ToDictionary(
                    e => e.Key,
                    e => new
                    {
                        status = e.Value.Status.ToString().ToLowerInvariant(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.TotalMilliseconds,
                        tags = e.Value.Tags,
                        data = e.Value.Data,
                        exception = e.Value.Exception?.Message
                    })
            };

            return result.Status == HealthStatus.Healthy 
                ? Ok(response)
                : StatusCode(503, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detailed health check failed");
            return StatusCode(503, new { 
                status = "unhealthy", 
                error = "Detailed health check failed", 
                timestamp = DateTime.UtcNow 
            });
        }
    }
} 