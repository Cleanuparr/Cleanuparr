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
                .ToListAsync();

            // Auto-create missing seeker instance configs for new arr instances
            foreach (var instance in arrInstances)
            {
                if (seekerInstanceConfigs.All(s => s.ArrInstanceId != instance.Id))
                {
                    var newConfig = new SeekerInstanceConfig
                    {
                        ArrInstanceId = instance.Id,
                        Enabled = false,
                    };
                    _dataContext.SeekerInstanceConfigs.Add(newConfig);
                    seekerInstanceConfigs.Add(newConfig);
                }
            }
            await _dataContext.SaveChangesAsync();

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
                    ArrInstanceEnabled = instance.Enabled,
                    ActiveDownloadLimit = seekerConfig?.ActiveDownloadLimit ?? 0,
                };
            }).ToList();

            var response = new SeekerConfigResponse
            {
                SearchEnabled = config.SearchEnabled,
                SearchInterval = config.SearchInterval,
                ProactiveSearchEnabled = config.ProactiveSearchEnabled,
                SelectionStrategy = config.SelectionStrategy,
                MonitoredOnly = config.MonitoredOnly,
                UseCutoff = config.UseCutoff,
                UseCustomFormatScore = config.UseCustomFormatScore,
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

            ushort previousInterval = config.SearchInterval;
            bool previousUseCustomFormatScore = config.UseCustomFormatScore;

            request.ApplyTo(config);
            config.Validate();

            if (request.ProactiveSearchEnabled && request.Instances.Count > 0 && !request.Instances.Any(i => i.Enabled))
            {
                throw new Domain.Exceptions.ValidationException(
                    "At least one instance must be enabled when proactive search is enabled");
            }

            // Sync instance configs
            var existingInstanceConfigs = await _dataContext.SeekerInstanceConfigs.ToListAsync();

            foreach (var instanceReq in request.Instances)
            {
                var existing = existingInstanceConfigs
                    .FirstOrDefault(e => e.ArrInstanceId == instanceReq.ArrInstanceId);

                if (existing is not null)
                {
                    existing.Enabled = instanceReq.Enabled;
                    existing.SkipTags = instanceReq.SkipTags;
                    existing.ActiveDownloadLimit = instanceReq.ActiveDownloadLimit;
                }
                else
                {
                    _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
                    {
                        ArrInstanceId = instanceReq.ArrInstanceId,
                        Enabled = instanceReq.Enabled,
                        SkipTags = instanceReq.SkipTags,
                        ActiveDownloadLimit = instanceReq.ActiveDownloadLimit,
                    });
                }
            }

            await _dataContext.SaveChangesAsync();

            // Update Quartz trigger if SearchInterval changed
            if (config.SearchInterval != previousInterval)
            {
                _logger.LogInformation("Search interval changed from {Old} to {New} minutes, updating Seeker schedule",
                    previousInterval, config.SearchInterval);
                await _jobManagementService.StartJob(JobType.Seeker, null, config.ToCronExpression());
            }

            // Toggle CustomFormatScoreSyncer job when UseCustomFormatScore changes
            if (config.UseCustomFormatScore != previousUseCustomFormatScore)
            {
                if (config.UseCustomFormatScore)
                {
                    _logger.LogInformation("UseCustomFormatScore enabled, starting CustomFormatScoreSyncer job");
                    await _jobManagementService.StartJob(JobType.CustomFormatScoreSyncer, null, Constants.CustomFormatScoreSyncerCron);
                    await _jobManagementService.TriggerJobOnce(JobType.CustomFormatScoreSyncer);
                }
                else
                {
                    _logger.LogInformation("UseCustomFormatScore disabled, stopping CustomFormatScoreSyncer job");
                    await _jobManagementService.StopJob(JobType.CustomFormatScoreSyncer);
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
