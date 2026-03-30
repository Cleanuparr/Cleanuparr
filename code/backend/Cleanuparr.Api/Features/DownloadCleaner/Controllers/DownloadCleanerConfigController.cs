using System.ComponentModel.DataAnnotations;

using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
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

            var clients = new List<object>();

            foreach (var client in downloadClients)
            {
                var seedingRules = await GetSeedingRulesForClient(client);
                var unlinkedConfig = await _dataContext.UnlinkedConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.DownloadClientConfigId == client.Id);

                clients.Add(new
                {
                    downloadClientId = client.Id,
                    downloadClientName = client.Name,
                    downloadClientEnabled = client.Enabled,
                    downloadClientTypeName = client.TypeName,
                    seedingRules = seedingRules.Select(r => new
                    {
                        id = r.Id,
                        name = r.Name,
                        privacyType = r.PrivacyType,
                        maxRatio = r.MaxRatio,
                        minSeedTime = r.MinSeedTime,
                        maxSeedTime = r.MaxSeedTime,
                        deleteSourceFiles = r.DeleteSourceFiles,
                    }),
                    unlinkedConfig = unlinkedConfig is not null
                        ? new
                        {
                            enabled = unlinkedConfig.Enabled,
                            targetCategory = unlinkedConfig.TargetCategory,
                            useTag = unlinkedConfig.UseTag,
                            ignoredRootDirs = unlinkedConfig.IgnoredRootDirs,
                            categories = unlinkedConfig.Categories,
                            downloadDirectorySource = unlinkedConfig.DownloadDirectorySource,
                            downloadDirectoryTarget = unlinkedConfig.DownloadDirectoryTarget,
                        }
                        : null,
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

    private async Task<List<ISeedingRule>> GetSeedingRulesForClient(DownloadClientConfig client)
    {
        return client.TypeName switch
        {
            DownloadClientTypeName.qBittorrent => (await _dataContext.QBitSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Deluge => (await _dataContext.DelugeSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Transmission => (await _dataContext.TransmissionSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.uTorrent => (await _dataContext.UTorrentSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.rTorrent => (await _dataContext.RTorrentSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            _ => []
        };
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
