using System;
using System.Linq;

using Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.DownloadClient.Controllers;

[ApiController]
[Route("api/configuration")]
public sealed class DownloadClientController : ControllerBase
{
    private readonly ILogger<DownloadClientController> _logger;
    private readonly DataContext _dataContext;
    private readonly IDynamicHttpClientFactory _dynamicHttpClientFactory;
    private readonly IDownloadServiceFactory _downloadServiceFactory;

    public DownloadClientController(
        ILogger<DownloadClientController> logger,
        DataContext dataContext,
        IDynamicHttpClientFactory dynamicHttpClientFactory,
        IDownloadServiceFactory downloadServiceFactory)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dynamicHttpClientFactory = dynamicHttpClientFactory;
        _downloadServiceFactory = downloadServiceFactory;
    }

    [HttpGet("download_client")]
    public async Task<IActionResult> GetDownloadClientConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var clients = await _dataContext.DownloadClients
                .AsNoTracking()
                .ToListAsync();

            clients = clients
                .OrderBy(c => c.TypeName)
                .ThenBy(c => c.Name)
                .ToList();

            return Ok(new { clients });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("download_client")]
    public async Task<IActionResult> CreateDownloadClientConfig([FromBody] CreateDownloadClientRequest newClient)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            newClient.Validate();

            var clientConfig = newClient.ToEntity();

            _dataContext.DownloadClients.Add(clientConfig);
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDownloadClientConfig), new { id = clientConfig.Id }, clientConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create download client");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("download_client/{id}")]
    public async Task<IActionResult> UpdateDownloadClientConfig(Guid id, [FromBody] UpdateDownloadClientRequest updatedClient)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            updatedClient.Validate();

            var existingClient = await _dataContext.DownloadClients
                .FirstOrDefaultAsync(c => c.Id == id);

            if (existingClient is null)
            {
                return NotFound($"Download client with ID {id} not found");
            }

            var clientToPersist = updatedClient.ApplyTo(existingClient);

            _dataContext.Entry(existingClient).CurrentValues.SetValues(clientToPersist);
            await _dataContext.SaveChangesAsync();

            return Ok(clientToPersist);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update download client with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("download_client/{id}")]
    public async Task<IActionResult> DeleteDownloadClientConfig(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingClient = await _dataContext.DownloadClients
                .FirstOrDefaultAsync(c => c.Id == id);

            if (existingClient is null)
            {
                return NotFound($"Download client with ID {id} not found");
            }

            _dataContext.DownloadClients.Remove(existingClient);
            await _dataContext.SaveChangesAsync();

            var clientName = $"DownloadClient_{id}";
            _dynamicHttpClientFactory.UnregisterConfiguration(clientName);

            _logger.LogInformation("Removed HTTP client configuration for deleted download client {ClientName}", clientName);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete download client with ID {Id}", id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("download_client/test")]
    public async Task<IActionResult> TestDownloadClient([FromBody] TestDownloadClientRequest request)
    {
        try
        {
            request.Validate();

            var testConfig = request.ToTestConfig();
            using var downloadService = _downloadServiceFactory.GetDownloadService(testConfig);
            var healthResult = await downloadService.HealthCheckAsync();

            if (healthResult.IsHealthy)
            {
                return Ok(new
                {
                    Message = $"Connection to {request.TypeName} successful",
                    ResponseTime = healthResult.ResponseTime.TotalMilliseconds
                });
            }

            return BadRequest(new { Message = healthResult.ErrorMessage ?? "Connection failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test {TypeName} client connection", request.TypeName);
            return BadRequest(new { Message = $"Connection failed: {ex.Message}" });
        }
    }
}
