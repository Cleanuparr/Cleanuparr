using System;
using System.Threading.Tasks;

using Cleanuparr.Api.Features.BlacklistSync.Contracts.Requests;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.BlacklistSync.Controllers;

[ApiController]
[Route("api/configuration")]
public sealed class BlacklistSyncConfigController : ControllerBase
{
    private readonly ILogger<BlacklistSyncConfigController> _logger;
    private readonly DataContext _dataContext;
    private readonly IJobManagementService _jobManagementService;

    public BlacklistSyncConfigController(
        ILogger<BlacklistSyncConfigController> logger,
        DataContext dataContext,
        IJobManagementService jobManagementService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _jobManagementService = jobManagementService;
    }

    [HttpGet("blacklist_sync")]
    public async Task<IActionResult> GetBlacklistSyncConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.BlacklistSyncConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("blacklist_sync")]
    public async Task<IActionResult> UpdateBlacklistSyncConfig([FromBody] UpdateBlacklistSyncConfigRequest request)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.BlacklistSyncConfigs
                .FirstAsync();

            bool enabledChanged = config.Enabled != request.Enabled;
            bool becameEnabled = !config.Enabled && request.Enabled;
            bool pathChanged = request.HasPathChanged(config.BlacklistPath);

            request.ApplyTo(config);
            config.Validate();

            await _dataContext.SaveChangesAsync();

            if (enabledChanged)
            {
                if (becameEnabled)
                {
                    _logger.LogInformation("BlacklistSynchronizer enabled, starting job");
                    await _jobManagementService.StartJob(JobType.BlacklistSynchronizer, null, config.CronExpression);
                    await _jobManagementService.TriggerJobOnce(JobType.BlacklistSynchronizer);
                }
                else
                {
                    _logger.LogInformation("BlacklistSynchronizer disabled, stopping the job");
                    await _jobManagementService.StopJob(JobType.BlacklistSynchronizer);
                }
            }
            else if (pathChanged && config.Enabled)
            {
                _logger.LogDebug("BlacklistSynchronizer path changed");
                await _jobManagementService.TriggerJobOnce(JobType.BlacklistSynchronizer);
            }

            return Ok(new { Message = "BlacklistSynchronizer configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save BlacklistSync configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
