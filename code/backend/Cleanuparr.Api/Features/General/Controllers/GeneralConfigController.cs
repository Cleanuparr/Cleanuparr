using System;
using System.Linq;
using System.Threading.Tasks;

using Cleanuparr.Api.Features.General.Contracts.Requests;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.General.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize]
public sealed class GeneralConfigController : ControllerBase
{
    private readonly ILogger<GeneralConfigController> _logger;
    private readonly DataContext _dataContext;

    public GeneralConfigController(
        ILogger<GeneralConfigController> logger,
        DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    [HttpGet("general")]
    public async Task<IActionResult> GetGeneralConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.GeneralConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("general")]
    public async Task<IActionResult> UpdateGeneralConfig(
        [FromBody] UpdateGeneralConfigRequest request,
        [FromServices] EventsContext eventsContext)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.GeneralConfigs
                .FirstAsync();

            bool wasDryRun = config.DryRun;

            request.ApplyTo(config, HttpContext.RequestServices, _logger);

            await _dataContext.SaveChangesAsync();

            if (wasDryRun && !config.DryRun)
            {
                await using var transaction = await eventsContext.Database.BeginTransactionAsync();

                try
                {
                    var deletedStrikes = await eventsContext.Strikes
                        .Where(s => s.IsDryRun)
                        .ExecuteDeleteAsync();
                    var deletedEvents = await eventsContext.Events
                        .Where(e => e.IsDryRun)
                        .ExecuteDeleteAsync();
                    var deletedManualEvents = await eventsContext.ManualEvents
                        .Where(e => e.IsDryRun)
                        .ExecuteDeleteAsync();
                    var deletedItems = await eventsContext.DownloadItems
                        .Where(d => !d.Strikes.Any())
                        .ExecuteDeleteAsync();

                    _logger.LogWarning(
                        "Dry run disabled — purged dry-run data: {Strikes} strikes, {Events} events, {ManualEvents} manual events, {Items} orphaned download items removed",
                        deletedStrikes, deletedEvents, deletedManualEvents, deletedItems);
                    
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return Ok(new { Message = "General configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save General configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("strikes/purge")]
    public async Task<IActionResult> PurgeAllStrikes(
        [FromServices] EventsContext eventsContext)
    {
        var deletedStrikes = await eventsContext.Strikes.ExecuteDeleteAsync();
        var deletedItems = await eventsContext.DownloadItems
            .Where(d => !d.Strikes.Any())
            .ExecuteDeleteAsync();

        _logger.LogWarning("Purged all strikes: {strikes} strikes, {items} download items removed",
            deletedStrikes, deletedItems);

        return Ok(new { DeletedStrikes = deletedStrikes, DeletedItems = deletedItems });
    }
}
