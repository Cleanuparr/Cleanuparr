using System;
using System.Linq;
using System.Threading.Tasks;

using Cleanuparr.Api.Features.General.Contracts.Requests;
using Cleanuparr.Api.Features.General.Contracts.Responses;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Persistence.Models.Configuration.General;
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
            GeneralConfig config = await _dataContext.GeneralConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(GeneralConfigResponse.From(config));
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

                    // An item with real strikes survives the purge, carrying whatever the dry run flagged on it.
                    var clearedFlags = await eventsContext.DownloadItems
                        .Where(d => d.IsMarkedForRemoval || d.IsRemoved || d.IsReturning)
                        .ExecuteUpdateAsync(setter => setter
                            .SetProperty(d => d.IsMarkedForRemoval, false)
                            .SetProperty(d => d.IsRemoved, false)
                            .SetProperty(d => d.IsReturning, false));

                    var deletedHistory = await eventsContext.SeekerHistory
                        .Where(h => h.IsDryRun)
                        .ExecuteDeleteAsync();

                    _logger.LogWarning(
                        "Dry run disabled, purged dry-run data: {Strikes} strikes, {Events} events, {ManualEvents} manual events, {Items} orphaned download items, {History} search history entries removed, {Flags} download items reset",
                        deletedStrikes, deletedEvents, deletedManualEvents, deletedItems, deletedHistory, clearedFlags);

                    await transaction.CommitAsync();

                    Striker.RecurringHashes.Clear();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return Ok(new { Message = "General configuration updated successfully" });
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
