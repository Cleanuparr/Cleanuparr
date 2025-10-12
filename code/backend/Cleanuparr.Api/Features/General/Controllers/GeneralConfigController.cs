using System;
using System.Linq;
using System.Threading.Tasks;

using Cleanuparr.Api.Features.General.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.General.Controllers;

[ApiController]
[Route("api/configuration")]
public sealed class GeneralConfigController : ControllerBase
{
    private readonly ILogger<GeneralConfigController> _logger;
    private readonly DataContext _dataContext;
    private readonly MemoryCache _cache;

    public GeneralConfigController(
        ILogger<GeneralConfigController> logger,
        DataContext dataContext,
        MemoryCache cache)
    {
        _logger = logger;
        _dataContext = dataContext;
        _cache = cache;
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
    public async Task<IActionResult> UpdateGeneralConfig([FromBody] UpdateGeneralConfigRequest request)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.GeneralConfigs
                .FirstAsync();

            bool wasDryRun = config.DryRun;

            request.ApplyTo(config, HttpContext.RequestServices, _logger);

            await _dataContext.SaveChangesAsync();

            ClearStrikesCacheIfNeeded(wasDryRun, config.DryRun);

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

    private void ClearStrikesCacheIfNeeded(bool wasDryRun, bool isDryRun)
    {
        if (!wasDryRun || isDryRun)
        {
            return;
        }

        List<object> keys;

        // Remove strikes
        foreach (string strikeType in Enum.GetNames(typeof(StrikeType)))
        {
            keys = _cache.Keys
                .Where(key => key.ToString()?.StartsWith(strikeType, StringComparison.InvariantCultureIgnoreCase) is true)
                .ToList();

            foreach (object key in keys)
            {
                _cache.Remove(key);
            }

            _logger.LogTrace("Removed all cache entries for strike type: {StrikeType}", strikeType);
        }

        // Remove strike cache items
        keys = _cache.Keys
            .Where(key => key.ToString()?.StartsWith("item_", StringComparison.InvariantCultureIgnoreCase) is true)
            .ToList();
        
        foreach (object key in keys)
        {
            _cache.Remove(key);
        }
    }
}
