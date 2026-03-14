using Cleanuparr.Api.Features.Seeker.Contracts.Requests;
using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Seeker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Seeker.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize]
public sealed class SeekerConfigController : ControllerBase
{
    private readonly ILogger<SeekerConfigController> _logger;
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagementService;

    public SeekerConfigController(
        ILogger<SeekerConfigController> logger,
        DataContext dataContext,
        IJobManagementService jobManagementService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _jobManagementService = jobManagementService;
    }

    [HttpGet("seeker")]
    public async Task<IActionResult> GetSeekerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.SeekerConfigs
                .AsNoTracking()
                .FirstAsync();

            // Get all Sonarr/Radarr instances with their seeker configs
            var arrInstances = await _dataContext.ArrInstances
                .AsNoTracking()
                .Include(a => a.ArrConfig)
                .Where(a => a.ArrConfig.Type == InstanceType.Sonarr || a.ArrConfig.Type == InstanceType.Radarr)
                .ToListAsync();

            var seekerInstanceConfigs = await _dataContext.SeekerInstanceConfigs
                .AsNoTracking()
                .ToListAsync();

            var instanceResponses = arrInstances.Select(instance =>
            {
                var seekerConfig = seekerInstanceConfigs.FirstOrDefault(s => s.ArrInstanceId == instance.Id);
                return new SeekerInstanceConfigResponse
                {
                    ArrInstanceId = instance.Id,
                    InstanceName = instance.Name,
                    InstanceType = instance.ArrConfig.Type,
                    Enabled = seekerConfig?.Enabled ?? false,
                    SkipTags = seekerConfig?.SkipTags ?? [],
                    LastProcessedAt = seekerConfig?.LastProcessedAt,
                };
            }).ToList();

            var response = new SeekerConfigResponse
            {
                Enabled = config.Enabled,
                CronExpression = config.CronExpression,
                UseAdvancedScheduling = config.UseAdvancedScheduling,
                SelectionStrategy = config.SelectionStrategy,
                MonitoredOnly = config.MonitoredOnly,
                UseCutoff = config.UseCutoff,
                UseRoundRobin = config.UseRoundRobin,
                Instances = instanceResponses,
            };

            return Ok(response);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("seeker")]
    public async Task<IActionResult> UpdateSeekerConfig([FromBody] UpdateSeekerConfigRequest request)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.SeekerConfigs.FirstAsync();

            bool enabledChanged = config.Enabled != request.Enabled;
            bool becameEnabled = !config.Enabled && request.Enabled;

            request.ApplyTo(config);
            config.Validate();

            if (request.Enabled && request.Instances.Count > 0 && !request.Instances.Any(i => i.Enabled))
            {
                throw new Domain.Exceptions.ValidationException(
                    "At least one instance must be enabled when the Seeker is enabled");
            }

            // Sync instance configs
            var existingInstanceConfigs = await _dataContext.SeekerInstanceConfigs.ToListAsync();

            foreach (var instanceReq in request.Instances)
            {
                var existing = existingInstanceConfigs
                    .FirstOrDefault(e => e.ArrInstanceId == instanceReq.ArrInstanceId);

                if (existing is not null)
                {
                    if (!instanceReq.Enabled)
                    {
                        // Remove disabled instance config
                        _dataContext.SeekerInstanceConfigs.Remove(existing);
                    }
                    else
                    {
                        existing.Enabled = instanceReq.Enabled;
                        existing.SkipTags = instanceReq.SkipTags;
                    }
                }
                else if (instanceReq.Enabled)
                {
                    // Create new instance config
                    _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
                    {
                        ArrInstanceId = instanceReq.ArrInstanceId,
                        Enabled = instanceReq.Enabled,
                        SkipTags = instanceReq.SkipTags,
                    });
                }
            }

            await _dataContext.SaveChangesAsync();

            if (enabledChanged)
            {
                if (becameEnabled)
                {
                    _logger.LogInformation("Seeker enabled, starting job");
                    await _jobManagementService.StartJob(JobType.Seeker, null, config.CronExpression);
                }
                else
                {
                    _logger.LogInformation("Seeker disabled, stopping the job");
                    await _jobManagementService.StopJob(JobType.Seeker);
                }
            }

            return Ok(new { Message = "Seeker configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Seeker configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
