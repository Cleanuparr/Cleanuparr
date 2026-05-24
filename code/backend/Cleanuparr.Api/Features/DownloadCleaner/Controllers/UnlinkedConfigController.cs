using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.DownloadCleaner.Controllers;

[ApiController]
[Route("api/unlinked-config")]
[Authorize]
public class UnlinkedConfigController : ControllerBase
{
    private readonly ILogger<UnlinkedConfigController> _logger;
    private readonly DataContext _dataContext;

    public UnlinkedConfigController(
        ILogger<UnlinkedConfigController> logger,
        DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    [HttpGet("{downloadClientId}")]
    public async Task<IActionResult> GetUnlinkedConfig(Guid downloadClientId)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var client = await _dataContext.DownloadClients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == downloadClientId);

            if (client is null)
            {
                return NotFound(new { Message = $"Download client with ID {downloadClientId} not found" });
            }

            var config = await _dataContext.UnlinkedConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.DownloadClientConfigId == downloadClientId);

            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("{downloadClientId}")]
    public async Task<IActionResult> UpdateUnlinkedConfig(Guid downloadClientId, [FromBody] UnlinkedConfigRequest dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var client = await _dataContext.DownloadClients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == downloadClientId);

            if (client is null)
            {
                return NotFound(new { Message = $"Download client with ID {downloadClientId} not found" });
            }

            var existing = await _dataContext.UnlinkedConfigs
                .FirstOrDefaultAsync(u => u.DownloadClientConfigId == downloadClientId);

            if (existing is null)
            {
                existing = new UnlinkedConfig
                {
                    DownloadClientConfigId = downloadClientId,
                };
                _dataContext.UnlinkedConfigs.Add(existing);
            }

            existing.Enabled = dto.Enabled;
            existing.TargetCategory = dto.TargetCategory;
            existing.UseTag = dto.UseTag;
            existing.IgnoredRootDirs = dto.IgnoredRootDirs;
            existing.Categories = dto.Categories;

            existing.Validate();

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Updated unlinked config for client {ClientId}", downloadClientId);

            return Ok(existing);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
