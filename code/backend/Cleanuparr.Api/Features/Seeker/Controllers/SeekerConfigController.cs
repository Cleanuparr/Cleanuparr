using Cleanuparr.Api.Features.Seeker.Contracts.Requests;
using Cleanuparr.Shared.Helpers;
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
        var config = await _dataContext.SeekerConfigs
            .AsNoTracking()
            .FirstAsync();

        // Get all Sonarr/Radarr instances with their seeker configs
        var arrInstances = await _dataContext.ArrInstances
            .AsNoTracking()
            .Include(a => a.ArrConfig)
            .Where(a => a.ArrConfig.Type == InstanceType.Sonarr || a.ArrConfig.Type == InstanceType.Radarr)
            .ToListAsync();

        var arrInstanceIds = arrInstances.Select(a => a.Id).ToHashSet();
        var seekerInstanceConfigs = await _dataContext.SeekerInstanceConfigs
            .AsNoTracking()
            .Where(s => arrInstanceIds.Contains(s.ArrInstanceId))
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
                ArrInstanceEnabled = instance.Enabled,
                ActiveDownloadLimit = seekerConfig?.ActiveDownloadLimit ?? 3,
                MinCycleTimeDays = seekerConfig?.MinCycleTimeDays ?? 7,
                MonitoredOnly = seekerConfig?.MonitoredOnly ?? true,
                UseCutoff = seekerConfig?.UseCutoff ?? false,
                UseCustomFormatScore = seekerConfig?.UseCustomFormatScore ?? false,
            };
        }).ToList();

        var response = new SeekerConfigResponse
        {
            SearchEnabled = config.SearchEnabled,
            SearchInterval = config.SearchInterval,
            ProactiveSearchEnabled = config.ProactiveSearchEnabled,
            SelectionStrategy = config.SelectionStrategy,
            UseRoundRobin = config.UseRoundRobin,
            PostReleaseGraceHours = config.PostReleaseGraceHours,
            Instances = instanceResponses,
        };

        return Ok(response);
    }

    [HttpPut("seeker")]
    public async Task<IActionResult> UpdateSeekerConfig([FromBody] UpdateSeekerConfigRequest request)
    {
        if (!await DataContext.Lock.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            return StatusCode(503, "Database is busy, please try again");
        }

        try
        {
            var config = await _dataContext.SeekerConfigs.FirstAsync();

            ushort previousInterval = config.SearchInterval;
            bool previousSearchEnabled = config.SearchEnabled;
            bool previousProactiveSearchEnabled = config.ProactiveSearchEnabled;

            request.ApplyTo(config);
            config.Validate();

            if (request.ProactiveSearchEnabled && !request.Instances.Any(i => i.Enabled))
            {
                throw new Domain.Exceptions.ValidationException(
                    "At least one instance must be enabled when proactive search is enabled");
            }

            // Sync instance configs
            var existingInstanceConfigs = await _dataContext.SeekerInstanceConfigs
                .Include(e => e.ArrInstance)
                .ToListAsync();
            bool previousAnyUseCustomFormatScore = existingInstanceConfigs.Any(e => e.Enabled && e.ArrInstance.Enabled && e.UseCustomFormatScore);

            foreach (var instanceReq in request.Instances)
            {
                var existing = existingInstanceConfigs
                    .FirstOrDefault(e => e.ArrInstanceId == instanceReq.ArrInstanceId);

                if (existing is not null)
                {
                    existing.Enabled = instanceReq.Enabled;
                    existing.SkipTags = instanceReq.SkipTags;
                    existing.ActiveDownloadLimit = instanceReq.ActiveDownloadLimit;
                    existing.MinCycleTimeDays = instanceReq.MinCycleTimeDays;
                    existing.MonitoredOnly = instanceReq.MonitoredOnly;
                    existing.UseCutoff = instanceReq.UseCutoff;
                    existing.UseCustomFormatScore = instanceReq.UseCustomFormatScore;
                }
                else
                {
                    _dataContext.SeekerInstanceConfigs.Add(new SeekerInstanceConfig
                    {
                        ArrInstanceId = instanceReq.ArrInstanceId,
                        Enabled = instanceReq.Enabled,
                        SkipTags = instanceReq.SkipTags,
                        ActiveDownloadLimit = instanceReq.ActiveDownloadLimit,
                        MinCycleTimeDays = instanceReq.MinCycleTimeDays,
                        MonitoredOnly = instanceReq.MonitoredOnly,
                        UseCutoff = instanceReq.UseCutoff,
                        UseCustomFormatScore = instanceReq.UseCustomFormatScore,
                    });
                }
            }

            await _dataContext.SaveChangesAsync();

            bool anyUseCustomFormatScore = await _dataContext.SeekerInstanceConfigs
                .AnyAsync(s => s.Enabled && s.ArrInstance.Enabled && s.UseCustomFormatScore);

            // Start/stop Seeker based on SearchEnabled toggle
            if (config.SearchEnabled != previousSearchEnabled)
            {
                if (config.SearchEnabled)
                {
                    _logger.LogInformation("SearchEnabled turned on, starting Seeker job");
                    await _jobManagementService.StartJob(JobType.Seeker, null, config.ToCronExpression());
                }
                else
                {
                    _logger.LogInformation("SearchEnabled turned off, stopping Seeker job");
                    await _jobManagementService.StopJob(JobType.Seeker);
                }
            }
            // Update Quartz trigger if SearchInterval changed (only while search is enabled)
            else if (config.SearchEnabled && config.SearchInterval != previousInterval)
            {
                _logger.LogInformation("Search interval changed from {Old} to {New} minutes, updating Seeker schedule",
                    previousInterval, config.SearchInterval);
                await _jobManagementService.StartJob(JobType.Seeker, null, config.ToCronExpression());
            }

            // Start/stop CustomFormatScoreSyncer
            bool syncerShouldBeRunning = anyUseCustomFormatScore && config.ProactiveSearchEnabled;
            bool syncerWasRunning = previousAnyUseCustomFormatScore && previousProactiveSearchEnabled;

            if (syncerShouldBeRunning != syncerWasRunning)
            {
                if (syncerShouldBeRunning)
                {
                    _logger.LogInformation("CustomFormatScoreSyncer conditions met, starting job");
                    await _jobManagementService.StartJob(JobType.CustomFormatScoreSyncer, null, Constants.CustomFormatScoreSyncerCron);
                    await _jobManagementService.TriggerJobOnce(JobType.CustomFormatScoreSyncer);
                }
                else
                {
                    _logger.LogInformation("CustomFormatScoreSyncer conditions no longer met, stopping job");
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
