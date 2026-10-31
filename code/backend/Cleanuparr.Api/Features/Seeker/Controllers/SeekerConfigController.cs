using Cleanuparr.Api.Extensions;
using Cleanuparr.Api.Features.Seeker.Contracts.Requests;
using Cleanuparr.Shared.Helpers;
using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
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

        List<ArrInstance> arrInstances = (await _dataContext.ArrInstances
                .AsNoTracking()
                .Include(a => a.ArrConfig)
                .ToListAsync())
            .Where(SeekerApps.IsSupported)
            .ToList();

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
                InstanceVersion = instance.Version,
                Enabled = seekerConfig?.Enabled ?? false,
                SkipTags = seekerConfig?.SkipTags ?? [],
                IncludeTags = seekerConfig?.IncludeTags ?? [],
                LastProcessedAt = seekerConfig?.LastProcessedAt,
                ArrInstanceEnabled = instance.Enabled,
                ActiveDownloadLimit = seekerConfig?.ActiveDownloadLimit ?? 3,
                IgnoreStruckDownloads = seekerConfig?.IgnoreStruckDownloads ?? false,
                MinCycleTimeDays = seekerConfig?.MinCycleTimeDays ?? 7,
                MonitoredOnly = seekerConfig?.MonitoredOnly ?? true,
                UseCutoff = seekerConfig?.UseCutoff ?? false,
                UseCustomFormatScore = seekerConfig?.UseCustomFormatScore ?? false,
                RecentGrabWindowHours = seekerConfig?.RecentGrabWindowHours ?? 24,
                SonarrSearchMode = seekerConfig?.SonarrSearchMode ?? SonarrSearchMode.Auto,
                RadarrReleaseGate = seekerConfig?.RadarrReleaseGate ?? RadarrReleaseGate.ReleaseDate,
                RestUnitsEnabled = seekerConfig?.RestUnitsEnabled ?? false,
                RestMaxDays = seekerConfig?.RestMaxDays ?? 30,
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
            QuietHoursEnabled = config.QuietHoursEnabled,
            QuietHoursStart = config.QuietHoursStart,
            QuietHoursEnd = config.QuietHoursEnd,
            Instances = instanceResponses,
        };

        return Ok(response);
    }

    [HttpPut("seeker")]
    public async Task<IActionResult> UpdateSeekerConfig([FromBody] UpdateSeekerConfigRequest request)
    {
        if (!await DataContext.Lock.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            return this.ProblemResult(StatusCodes.Status503ServiceUnavailable, "Database is busy, please try again");
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
                throw new Domain.Exceptions.CustomValidationException(
                    "At least one instance must be enabled when proactive search is enabled");
            }

            // Sync instance configs
            var existingInstanceConfigs = await _dataContext.SeekerInstanceConfigs
                .Include(e => e.ArrInstance)
                .ToListAsync();
            Dictionary<Guid, ArrInstance> arrInstancesById = await _dataContext.ArrInstances
                .AsNoTracking()
                .Include(a => a.ArrConfig)
                .ToDictionaryAsync(a => a.Id);
            bool previousAnyUseCustomFormatScore = existingInstanceConfigs.Any(e => e.Enabled && e.ArrInstance.Enabled && e.UseCustomFormatScore);

            foreach (var instanceReq in request.Instances)
            {
                bool useCustomFormatScore = instanceReq.UseCustomFormatScore
                    && arrInstancesById.TryGetValue(instanceReq.ArrInstanceId, out ArrInstance? arrInstance)
                    && SeekerApps.ReadsCustomFormatScores(arrInstance);

                var existing = existingInstanceConfigs
                    .FirstOrDefault(e => e.ArrInstanceId == instanceReq.ArrInstanceId);

                if (existing is not null)
                {
                    existing.Enabled = instanceReq.Enabled;
                    existing.SkipTags = instanceReq.SkipTags;
                    existing.IncludeTags = instanceReq.IncludeTags;
                    existing.ActiveDownloadLimit = instanceReq.ActiveDownloadLimit;
                    existing.IgnoreStruckDownloads = instanceReq.IgnoreStruckDownloads;
                    existing.MinCycleTimeDays = instanceReq.MinCycleTimeDays;
                    existing.MonitoredOnly = instanceReq.MonitoredOnly;
                    existing.UseCutoff = instanceReq.UseCutoff;
                    existing.UseCustomFormatScore = useCustomFormatScore;
                    existing.RecentGrabWindowHours = instanceReq.RecentGrabWindowHours;
                    existing.SonarrSearchMode = instanceReq.SonarrSearchMode;
                    existing.RadarrReleaseGate = instanceReq.RadarrReleaseGate;
                    existing.RestUnitsEnabled = instanceReq.RestUnitsEnabled;
                    existing.RestMaxDays = instanceReq.RestMaxDays;
                    existing.Validate();
                }
                else
                {
                    SeekerInstanceConfig newInstanceConfig = new()
                    {
                        ArrInstanceId = instanceReq.ArrInstanceId,
                        Enabled = instanceReq.Enabled,
                        SkipTags = instanceReq.SkipTags,
                        IncludeTags = instanceReq.IncludeTags,
                        ActiveDownloadLimit = instanceReq.ActiveDownloadLimit,
                        IgnoreStruckDownloads = instanceReq.IgnoreStruckDownloads,
                        MinCycleTimeDays = instanceReq.MinCycleTimeDays,
                        MonitoredOnly = instanceReq.MonitoredOnly,
                        UseCutoff = instanceReq.UseCutoff,
                        UseCustomFormatScore = useCustomFormatScore,
                        RecentGrabWindowHours = instanceReq.RecentGrabWindowHours,
                        SonarrSearchMode = instanceReq.SonarrSearchMode,
                        RadarrReleaseGate = instanceReq.RadarrReleaseGate,
                        RestUnitsEnabled = instanceReq.RestUnitsEnabled,
                        RestMaxDays = instanceReq.RestMaxDays,
                    };
                    newInstanceConfig.Validate();
                    await _dataContext.SeekerInstanceConfigs.AddAsync(newInstanceConfig);
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
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
