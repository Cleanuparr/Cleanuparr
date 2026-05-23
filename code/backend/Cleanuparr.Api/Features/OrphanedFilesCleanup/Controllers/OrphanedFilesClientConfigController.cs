using Cleanuparr.Api.Features.OrphanedFilesCleanup.Contracts.Requests;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.OrphanedFilesCleanup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Api.Features.OrphanedFilesCleanup.Controllers;

[ApiController]
[Route("api/configuration/orphaned_files_cleanup/clients")]
[Authorize]
public sealed class OrphanedFilesClientConfigController : ControllerBase
{
    private readonly ILogger<OrphanedFilesClientConfigController> _logger;
    private readonly DataContext _dataContext;

    public OrphanedFilesClientConfigController(
        ILogger<OrphanedFilesClientConfigController> logger,
        DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    [HttpGet("{downloadClientId}")]
    public async Task<IActionResult> GetClientConfig(Guid downloadClientId)
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

            var config = await _dataContext.OrphanedFilesClientConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.DownloadClientConfigId == downloadClientId);

            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("{downloadClientId}")]
    public async Task<IActionResult> UpdateClientConfig(Guid downloadClientId, [FromBody] OrphanedFilesClientConfigRequest dto)
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

            var existing = await _dataContext.OrphanedFilesClientConfigs
                .FirstOrDefaultAsync(c => c.DownloadClientConfigId == downloadClientId);

            if (existing is null)
            {
                existing = new OrphanedFilesClientConfig
                {
                    DownloadClientConfigId = downloadClientId,
                };
                _dataContext.OrphanedFilesClientConfigs.Add(existing);
            }

            existing.Enabled = dto.Enabled;
            existing.ScanDirectories = dto.ScanDirectories;
            existing.OrphanedDirectory = dto.OrphanedDirectory;
            existing.ExcludePatterns = dto.ExcludePatterns;
            existing.MinFileAgeMinutes = dto.MinFileAgeMinutes;
            existing.EmptyAfterXDays = dto.EmptyAfterXDays;

            var siblings = await _dataContext.OrphanedFilesClientConfigs
                .AsNoTracking()
                .Where(c => c.DownloadClientConfigId != downloadClientId)
                .ToListAsync();

            existing.Validate(siblings);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Updated orphaned files client config for client {ClientId}", downloadClientId);

            return Ok(existing);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for orphaned files client config: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
