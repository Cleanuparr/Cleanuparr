using System;
using System.Linq;
using System.Threading.Tasks;

using Cleanuparr.Api.Features.General.Contracts.Requests;
using Cleanuparr.Api.Features.General.Contracts.Responses;
using Cleanuparr.Infrastructure.Features.DryRun;
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
    private readonly IDryRunPurger _dryRunPurger;

    public GeneralConfigController(
        ILogger<GeneralConfigController> logger,
        DataContext dataContext,
        IDryRunPurger dryRunPurger)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dryRunPurger = dryRunPurger;
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
        [FromBody] UpdateGeneralConfigRequest request)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.GeneralConfigs
                .FirstAsync();

            bool wasDryRun = config.DryRun;

            request.ApplyTo(config, HttpContext.RequestServices, _logger);

            if (wasDryRun && !config.DryRun)
            {
                // A failed purge must leave dry run on.
                await _dryRunPurger.PurgeAsync();
            }

            await _dataContext.SaveChangesAsync();

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
