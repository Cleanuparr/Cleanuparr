using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.DownloadCleaner.Controllers;

[ApiController]
[Route("api/orphaned-files-config")]
[Authorize]
public sealed class OrphanedFilesConfigController : ControllerBase
{
    private readonly ILogger<OrphanedFilesConfigController> _logger;
    private readonly DataContext _dataContext;

    public OrphanedFilesConfigController(
        ILogger<OrphanedFilesConfigController> logger,
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

            var config = await _dataContext.OrphanedFilesConfigs
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
    public async Task<IActionResult> UpdateClientConfig(Guid downloadClientId, [FromBody] OrphanedFilesConfigRequest dto)
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

            var existing = await _dataContext.OrphanedFilesConfigs
                .FirstOrDefaultAsync(c => c.DownloadClientConfigId == downloadClientId);

            var candidate = (existing ?? new OrphanedFilesConfig { DownloadClientConfigId = downloadClientId }) with
            {
                Enabled = dto.Enabled,
                ScanDirectories = dto.ScanDirectories,
                OrphanedDirectory = dto.OrphanedDirectory,
                ExcludePatterns = dto.ExcludePatterns,
                MinFileAgeHours = dto.MinFileAgeHours,
                PurgeAfterHours = dto.PurgeAfterHours,
            };

            var siblings = await _dataContext.OrphanedFilesConfigs
                .AsNoTracking()
                .Where(c => c.DownloadClientConfigId != downloadClientId)
                .ToListAsync();

            var otherDownloadClients = await _dataContext.DownloadClients
                .AsNoTracking()
                .Where(c => c.Id != downloadClientId)
                .ToListAsync();

            candidate.Validate(siblings, otherDownloadClients);

            if (existing is null)
            {
                _dataContext.OrphanedFilesConfigs.Add(candidate);
            }
            else
            {
                existing.Enabled = candidate.Enabled;
                existing.ScanDirectories = candidate.ScanDirectories;
                existing.OrphanedDirectory = candidate.OrphanedDirectory;
                existing.ExcludePatterns = candidate.ExcludePatterns;
                existing.MinFileAgeHours = candidate.MinFileAgeHours;
                existing.PurgeAfterHours = candidate.PurgeAfterHours;
            }

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Updated orphaned files client config for client {ClientId}", downloadClientId);

            return Ok(existing ?? candidate);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
