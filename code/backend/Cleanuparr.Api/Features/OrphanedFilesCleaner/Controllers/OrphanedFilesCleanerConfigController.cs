using System.ComponentModel.DataAnnotations;

using Cleanuparr.Api.Features.OrphanedFilesCleaner.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Utilities;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.OrphanedFilesCleaner.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize]
public sealed class OrphanedFilesCleanerConfigController : ControllerBase
{
    private readonly ILogger<OrphanedFilesCleanerConfigController> _logger;
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagementService;

    public OrphanedFilesCleanerConfigController(
        ILogger<OrphanedFilesCleanerConfigController> logger,
        DataContext dataContext,
        IJobManagementService jobManagementService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _jobManagementService = jobManagementService;
    }

    [HttpGet("orphaned_files_cleaner")]
    public async Task<IActionResult> GetOrphanedFilesCleanerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.OrphanedFilesCleanerConfigs
                .AsNoTracking()
                .FirstAsync();

            return Ok(new
            {
                config.Enabled,
                config.CronExpression,
                config.UseAdvancedScheduling,
                config.ScanDirectories,
                config.OrphanedDirectory,
                config.DownloadDirectorySource,
                config.DownloadDirectoryTarget,
                config.ExcludePatterns,
                config.MinFileAgeMinutes,
                config.MaxOrphanedFilesToProcess,
                config.EmptyAfterXDays,
            });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("orphaned_files_cleaner")]
    public async Task<IActionResult> UpdateOrphanedFilesCleanerConfig([FromBody] UpdateOrphanedFilesCleanerConfigRequest newConfigDto)
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

            var oldConfig = await _dataContext.OrphanedFilesCleanerConfigs.FirstAsync();

            oldConfig.Enabled = newConfigDto.Enabled;
            oldConfig.CronExpression = newConfigDto.CronExpression;
            oldConfig.UseAdvancedScheduling = newConfigDto.UseAdvancedScheduling;
            oldConfig.ScanDirectories = newConfigDto.ScanDirectories;
            oldConfig.OrphanedDirectory = newConfigDto.OrphanedDirectory;
            oldConfig.DownloadDirectorySource = newConfigDto.DownloadDirectorySource;
            oldConfig.DownloadDirectoryTarget = newConfigDto.DownloadDirectoryTarget;
            oldConfig.ExcludePatterns = newConfigDto.ExcludePatterns;
            oldConfig.MinFileAgeMinutes = newConfigDto.MinFileAgeMinutes;
            oldConfig.MaxOrphanedFilesToProcess = newConfigDto.MaxOrphanedFilesToProcess;
            oldConfig.EmptyAfterXDays = newConfigDto.EmptyAfterXDays;

            await _dataContext.SaveChangesAsync();

            await UpdateJobSchedule(oldConfig, JobType.OrphanedFilesCleaner);

            return Ok(new { Message = "OrphanedFilesCleaner configuration updated successfully" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save OrphanedFilesCleaner configuration");
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
