using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;

using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Utilities;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.DownloadCleaner.Controllers;

[ApiController]
[Route("api/configuration")]
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
                .Include(x => x.Categories)
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
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

            if (!string.IsNullOrEmpty(newConfigDto.CronExpression))
            {
                CronValidationHelper.ValidateCronExpression(newConfigDto.CronExpression);
            }

            if (newConfigDto.Enabled && newConfigDto.Categories.Any())
            {
                if (newConfigDto.Categories.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                {
                    throw new ValidationException("Duplicate category names found");
                }

                foreach (var categoryDto in newConfigDto.Categories)
                {
                    if (string.IsNullOrWhiteSpace(categoryDto.Name))
                    {
                        throw new ValidationException("Category name cannot be empty");
                    }

                    if (categoryDto is { MaxRatio: < 0, MaxSeedTime: < 0 })
                    {
                        throw new ValidationException("Either max ratio or max seed time must be enabled");
                    }

                    if (categoryDto.MinSeedTime < 0)
                    {
                        throw new ValidationException("Min seed time cannot be negative");
                    }
                }
            }

            if (newConfigDto.UnlinkedEnabled)
            {
                if (string.IsNullOrWhiteSpace(newConfigDto.UnlinkedTargetCategory))
                {
                    throw new ValidationException("Unlinked target category cannot be empty");
                }

                if (newConfigDto.UnlinkedCategories?.Count is null or 0)
                {
                    throw new ValidationException("Unlinked categories cannot be empty");
                }

                if (newConfigDto.UnlinkedCategories.Contains(newConfigDto.UnlinkedTargetCategory))
                {
                    throw new ValidationException("The unlinked target category should not be present in unlinked categories");
                }

                if (newConfigDto.UnlinkedCategories.Any(string.IsNullOrWhiteSpace))
                {
                    throw new ValidationException("Empty unlinked category filter found");
                }

                if (!string.IsNullOrEmpty(newConfigDto.UnlinkedIgnoredRootDir) && !Directory.Exists(newConfigDto.UnlinkedIgnoredRootDir))
                {
                    throw new ValidationException($"{newConfigDto.UnlinkedIgnoredRootDir} root directory does not exist");
                }
            }

            var oldConfig = await _dataContext.DownloadCleanerConfigs
                .Include(x => x.Categories)
                .FirstAsync();

            oldConfig.Enabled = newConfigDto.Enabled;
            oldConfig.CronExpression = newConfigDto.CronExpression;
            oldConfig.UseAdvancedScheduling = newConfigDto.UseAdvancedScheduling;
            oldConfig.DeletePrivate = newConfigDto.DeletePrivate;
            oldConfig.UnlinkedEnabled = newConfigDto.UnlinkedEnabled;
            oldConfig.UnlinkedTargetCategory = newConfigDto.UnlinkedTargetCategory;
            oldConfig.UnlinkedUseTag = newConfigDto.UnlinkedUseTag;
            oldConfig.UnlinkedIgnoredRootDir = newConfigDto.UnlinkedIgnoredRootDir;
            oldConfig.UnlinkedCategories = newConfigDto.UnlinkedCategories;
            oldConfig.IgnoredDownloads = newConfigDto.IgnoredDownloads;

            _dataContext.CleanCategories.RemoveRange(oldConfig.Categories);
            _dataContext.DownloadCleanerConfigs.Update(oldConfig);

            foreach (var categoryDto in newConfigDto.Categories)
            {
                _dataContext.CleanCategories.Add(new CleanCategory
                {
                    Name = categoryDto.Name,
                    MaxRatio = categoryDto.MaxRatio,
                    MinSeedTime = categoryDto.MinSeedTime,
                    MaxSeedTime = categoryDto.MaxSeedTime,
                    DownloadCleanerConfigId = oldConfig.Id
                });
            }

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
