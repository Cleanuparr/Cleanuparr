using System;
using System.Linq;
using System.Threading.Tasks;

using Cleanuparr.Api.Features.Arr.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Dtos;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.Arr.Controllers;

[ApiController]
[Route("api/configuration")]
public sealed class ArrConfigController : ControllerBase
{
    private readonly ILogger<ArrConfigController> _logger;
    private readonly DataContext _dataContext;

    public ArrConfigController(
        ILogger<ArrConfigController> logger,
        DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    [HttpGet("sonarr")]
    public Task<IActionResult> GetSonarrConfig() => GetArrConfig(InstanceType.Sonarr);

    [HttpGet("radarr")]
    public Task<IActionResult> GetRadarrConfig() => GetArrConfig(InstanceType.Radarr);

    [HttpGet("lidarr")]
    public Task<IActionResult> GetLidarrConfig() => GetArrConfig(InstanceType.Lidarr);

    [HttpGet("readarr")]
    public Task<IActionResult> GetReadarrConfig() => GetArrConfig(InstanceType.Readarr);

    [HttpGet("whisparr")]
    public Task<IActionResult> GetWhisparrConfig() => GetArrConfig(InstanceType.Whisparr);

    [HttpPut("sonarr")]
    public Task<IActionResult> UpdateSonarrConfig([FromBody] UpdateArrConfigRequest request)
        => UpdateArrConfig(InstanceType.Sonarr, request);

    [HttpPut("radarr")]
    public Task<IActionResult> UpdateRadarrConfig([FromBody] UpdateArrConfigRequest request)
        => UpdateArrConfig(InstanceType.Radarr, request);

    [HttpPut("lidarr")]
    public Task<IActionResult> UpdateLidarrConfig([FromBody] UpdateArrConfigRequest request)
        => UpdateArrConfig(InstanceType.Lidarr, request);

    [HttpPut("readarr")]
    public Task<IActionResult> UpdateReadarrConfig([FromBody] UpdateArrConfigRequest request)
        => UpdateArrConfig(InstanceType.Readarr, request);

    [HttpPut("whisparr")]
    public Task<IActionResult> UpdateWhisparrConfig([FromBody] UpdateArrConfigRequest request)
        => UpdateArrConfig(InstanceType.Whisparr, request);

    [HttpPost("sonarr/instances")]
    public Task<IActionResult> CreateSonarrInstance([FromBody] ArrInstanceRequest request)
        => CreateArrInstance(InstanceType.Sonarr, request);

    [HttpPut("sonarr/instances/{id}")]
    public Task<IActionResult> UpdateSonarrInstance(Guid id, [FromBody] ArrInstanceRequest request)
        => UpdateArrInstance(InstanceType.Sonarr, id, request);

    [HttpDelete("sonarr/instances/{id}")]
    public Task<IActionResult> DeleteSonarrInstance(Guid id)
        => DeleteArrInstance(InstanceType.Sonarr, id);

    [HttpPost("radarr/instances")]
    public Task<IActionResult> CreateRadarrInstance([FromBody] ArrInstanceRequest request)
        => CreateArrInstance(InstanceType.Radarr, request);

    [HttpPut("radarr/instances/{id}")]
    public Task<IActionResult> UpdateRadarrInstance(Guid id, [FromBody] ArrInstanceRequest request)
        => UpdateArrInstance(InstanceType.Radarr, id, request);

    [HttpDelete("radarr/instances/{id}")]
    public Task<IActionResult> DeleteRadarrInstance(Guid id)
        => DeleteArrInstance(InstanceType.Radarr, id);

    [HttpPost("lidarr/instances")]
    public Task<IActionResult> CreateLidarrInstance([FromBody] ArrInstanceRequest request)
        => CreateArrInstance(InstanceType.Lidarr, request);

    [HttpPut("lidarr/instances/{id}")]
    public Task<IActionResult> UpdateLidarrInstance(Guid id, [FromBody] ArrInstanceRequest request)
        => UpdateArrInstance(InstanceType.Lidarr, id, request);

    [HttpDelete("lidarr/instances/{id}")]
    public Task<IActionResult> DeleteLidarrInstance(Guid id)
        => DeleteArrInstance(InstanceType.Lidarr, id);

    [HttpPost("readarr/instances")]
    public Task<IActionResult> CreateReadarrInstance([FromBody] ArrInstanceRequest request)
        => CreateArrInstance(InstanceType.Readarr, request);

    [HttpPut("readarr/instances/{id}")]
    public Task<IActionResult> UpdateReadarrInstance(Guid id, [FromBody] ArrInstanceRequest request)
        => UpdateArrInstance(InstanceType.Readarr, id, request);

    [HttpDelete("readarr/instances/{id}")]
    public Task<IActionResult> DeleteReadarrInstance(Guid id)
        => DeleteArrInstance(InstanceType.Readarr, id);

    [HttpPost("whisparr/instances")]
    public Task<IActionResult> CreateWhisparrInstance([FromBody] ArrInstanceRequest request)
        => CreateArrInstance(InstanceType.Whisparr, request);

    [HttpPut("whisparr/instances/{id}")]
    public Task<IActionResult> UpdateWhisparrInstance(Guid id, [FromBody] ArrInstanceRequest request)
        => UpdateArrInstance(InstanceType.Whisparr, id, request);

    [HttpDelete("whisparr/instances/{id}")]
    public Task<IActionResult> DeleteWhisparrInstance(Guid id)
        => DeleteArrInstance(InstanceType.Whisparr, id);

    private async Task<IActionResult> GetArrConfig(InstanceType type)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == type);

            config.Instances = config.Instances
                .OrderBy(i => i.Name)
                .ToList();

            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task<IActionResult> UpdateArrConfig(InstanceType type, UpdateArrConfigRequest request)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == type);

            config.FailedImportMaxStrikes = request.FailedImportMaxStrikes;
            config.Validate();

            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = $"{type} configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save {Type} configuration", type);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task<IActionResult> CreateArrInstance(InstanceType type, ArrInstanceRequest request)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == type);

            var instance = request.ToEntity(config.Id);
            await _dataContext.ArrInstances.AddAsync(instance);
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(GetConfigActionName(type), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create {Type} instance", type);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task<IActionResult> UpdateArrInstance(InstanceType type, Guid id, ArrInstanceRequest request)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == type);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance is null)
            {
                return NotFound($"{type} instance with ID {id} not found");
            }

            request.ApplyTo(instance);

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update {Type} instance with ID {Id}", type, id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task<IActionResult> DeleteArrInstance(InstanceType type, Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == type);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance is null)
            {
                return NotFound($"{type} instance with ID {id} not found");
            }

            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Type} instance with ID {Id}", type, id);
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private static string GetConfigActionName(InstanceType type) => type switch
    {
        InstanceType.Sonarr => nameof(GetSonarrConfig),
        InstanceType.Radarr => nameof(GetRadarrConfig),
        InstanceType.Lidarr => nameof(GetLidarrConfig),
        InstanceType.Readarr => nameof(GetReadarrConfig),
        InstanceType.Whisparr => nameof(GetWhisparrConfig),
        _ => nameof(GetSonarrConfig),
    };
}
