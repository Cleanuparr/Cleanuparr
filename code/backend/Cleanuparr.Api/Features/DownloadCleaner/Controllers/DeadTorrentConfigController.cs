using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.DownloadCleaner.Controllers;

[ApiController]
[Route("api/dead-torrent-config")]
[Authorize]
public class DeadTorrentConfigController : ControllerBase
{
    private readonly ILogger<DeadTorrentConfigController> _logger;
    private readonly DataContext _dataContext;

    public DeadTorrentConfigController(
        ILogger<DeadTorrentConfigController> logger,
        DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    [HttpGet("{downloadClientId}")]
    public async Task<IActionResult> GetDeadTorrentConfig(Guid downloadClientId)
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

            var config = await _dataContext.DeadTorrentConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DownloadClientConfigId == downloadClientId);

            return Ok(config is null ? null : DeadTorrentConfigResponse.From(config));
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("{downloadClientId}")]
    public async Task<IActionResult> UpdateDeadTorrentConfig(Guid downloadClientId, [FromBody] DeadTorrentConfigRequest dto)
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

            if (dto.Enabled && client.TypeName is DownloadClientTypeName.rTorrent)
            {
                return BadRequest(new { Message = "Dead torrent handling is not supported for rTorrent (no seeder count available)" });
            }

            var existing = await _dataContext.DeadTorrentConfigs
                .FirstOrDefaultAsync(d => d.DownloadClientConfigId == downloadClientId);

            if (existing is null)
            {
                existing = new DeadTorrentConfig
                {
                    DownloadClientConfigId = downloadClientId,
                };
                _dataContext.DeadTorrentConfigs.Add(existing);
            }

            existing.Enabled = dto.Enabled;
            existing.TargetCategory = dto.TargetCategory;
            existing.UseTag = dto.UseTag;
            existing.MaxStrikes = dto.MaxStrikes;
            existing.Categories = dto.Categories;

            existing.Validate();

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Updated dead torrent config for client {ClientId}", downloadClientId);

            return Ok(DeadTorrentConfigResponse.From(existing));
        }
        catch (Cleanuparr.Domain.Exceptions.ValidationException ex)
        {
            _logger.LogWarning("Validation failed for dead torrent config update: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
