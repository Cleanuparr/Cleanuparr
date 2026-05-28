using System.ComponentModel.DataAnnotations;

using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Utilities;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.DownloadCleaner.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize]
public sealed class DownloadCleanerConfigController : ControllerBase
{
    private readonly ILogger<DownloadCleanerConfigController> _logger;
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagementService;

    public DownloadCleanerConfigController(
        ILogger<DownloadCleanerConfigController> logger,
        DataContext dataContext,
        IJobManagementService jobManagementService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _jobManagementService = jobManagementService;
    }

    [HttpGet("download_cleaner")]
    public async Task<IActionResult> GetDownloadCleanerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.DownloadCleanerConfigs
                .AsNoTracking()
                .FirstAsync();

            var downloadClients = await _dataContext.DownloadClients
                .AsNoTracking()
                .ToListAsync();

            var allQBitRules = await _dataContext.QBitSeedingRules.AsNoTracking().ToListAsync();
            var allDelugeRules = await _dataContext.DelugeSeedingRules.AsNoTracking().ToListAsync();
            var allTransmissionRules = await _dataContext.TransmissionSeedingRules.AsNoTracking().ToListAsync();
            var allUTorrentRules = await _dataContext.UTorrentSeedingRules.AsNoTracking().ToListAsync();
            var allRTorrentRules = await _dataContext.RTorrentSeedingRules.AsNoTracking().ToListAsync();
            var allUnlinkedConfigs = await _dataContext.UnlinkedConfigs.AsNoTracking().ToListAsync();
            var allOrphanedFilesConfigs = await _dataContext.OrphanedFilesConfigs.AsNoTracking().ToListAsync();

            var clients = new List<DownloadCleanerClientResponse>();

            foreach (var client in downloadClients)
            {
                List<ISeedingRule> seedingRules = SeedingRuleHelper
                    .FilterForClient(client, allQBitRules, allDelugeRules, allTransmissionRules, allUTorrentRules, allRTorrentRules);
                UnlinkedConfig? unlinkedConfig = allUnlinkedConfigs
                    .FirstOrDefault(u => u.DownloadClientConfigId == client.Id);
                OrphanedFilesConfig? orphanedFilesConfig = allOrphanedFilesConfigs
                    .FirstOrDefault(o => o.DownloadClientConfigId == client.Id);

                clients.Add(new DownloadCleanerClientResponse
                {
                    DownloadClientId = client.Id,
                    DownloadClientName = client.Name,
                    DownloadClientEnabled = client.Enabled,
                    DownloadClientTypeName = client.TypeName,
                    SeedingRules = seedingRules.Select(SeedingRuleResponse.From).ToList(),
                    UnlinkedConfig = unlinkedConfig is not null ? UnlinkedConfigResponse.From(unlinkedConfig) : null,
                    OrphanedFilesConfig = orphanedFilesConfig is not null ? OrphanedFilesConfigResponse.From(orphanedFilesConfig) : null,
                });
            }

            return Ok(new
            {
                config.Enabled,
                config.CronExpression,
                config.UseAdvancedScheduling,
                config.IgnoredDownloads,
                clients,
            });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("download_cleaner")]
    public async Task<IActionResult> UpdateDownloadCleanerConfig([FromBody] UpdateDownloadCleanerConfigRequest newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            if (newConfigDto is null)
            {
                throw new ValidationException("Request body cannot be null");
            }

            // Validate cron expression format
            if (!string.IsNullOrEmpty(newConfigDto.CronExpression))
            {
                CronValidationHelper.ValidateCronExpression(newConfigDto.CronExpression);
            }

            // Update global config only
            var oldConfig = await _dataContext.DownloadCleanerConfigs.FirstAsync();

            oldConfig.Enabled = newConfigDto.Enabled;
            oldConfig.CronExpression = newConfigDto.CronExpression;
            oldConfig.UseAdvancedScheduling = newConfigDto.UseAdvancedScheduling;
            oldConfig.IgnoredDownloads = newConfigDto.IgnoredDownloads;

            await _dataContext.SaveChangesAsync();

            await UpdateJobSchedule(oldConfig, JobType.DownloadCleaner);

            return Ok(new { Message = "DownloadCleaner configuration updated successfully" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save DownloadCleaner configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private async Task UpdateJobSchedule(IJobConfig config, JobType jobType)
    {
        if (config.Enabled)
        {
            if (!string.IsNullOrEmpty(config.CronExpression))
            {
                _logger.LogInformation("{name} is enabled, updating job schedule with cron expression: {CronExpression}",
                    jobType.ToString(), config.CronExpression);

                await _jobManagementService.StartJob(jobType, null, config.CronExpression);
            }
            else
            {
                _logger.LogWarning("{name} is enabled, but no cron expression was found in the configuration", jobType.ToString());
            }

            return;
        }

        _logger.LogInformation("{name} is disabled, stopping the job", jobType.ToString());
        await _jobManagementService.StopJob(jobType);
    }
}
