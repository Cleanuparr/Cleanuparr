using System.ComponentModel.DataAnnotations;

using Cleanuparr.Api.Features.OrphanedFilesCleanup.Contracts.Requests;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.OrphanedFilesCleanup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.OrphanedFilesCleanup.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize]
public sealed class OrphanedFilesCleanupConfigController : ControllerBase
{
    private readonly ILogger<OrphanedFilesCleanupConfigController> _logger;
    private readonly DataContext _dataContext;

    public OrphanedFilesCleanupConfigController(
        ILogger<OrphanedFilesCleanupConfigController> logger,
        DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    [HttpGet("orphaned_files_cleanup")]
    public async Task<IActionResult> GetOrphanedFilesCleanupConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.OrphanedFilesCleanupConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync()
                ?? new OrphanedFilesCleanupConfig();

            var downloadClients = await _dataContext.DownloadClients
                .AsNoTracking()
                .ToListAsync();

            var allClientConfigs = await _dataContext.OrphanedFilesClientConfigs
                .AsNoTracking()
                .ToListAsync();

            var clients = downloadClients.Select(client =>
            {
                var clientConfig = allClientConfigs.FirstOrDefault(c => c.DownloadClientConfigId == client.Id);
                return new
                {
                    downloadClientId = client.Id,
                    downloadClientName = client.Name,
                    downloadClientEnabled = client.Enabled,
                    clientConfig = clientConfig is null ? null : new
                    {
                        clientConfig.Enabled,
                        clientConfig.ScanDirectories,
                        clientConfig.OrphanedDirectory,
                    },
                };
            }).ToList();

            return Ok(new
            {
                config.ExcludePatterns,
                config.MinFileAgeMinutes,
                config.EmptyAfterXDays,
                clients,
            });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("orphaned_files_cleanup")]
    public async Task<IActionResult> UpdateOrphanedFilesCleanupConfig([FromBody] UpdateOrphanedFilesCleanupConfigRequest newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            if (newConfigDto is null)
            {
                throw new ValidationException("Request body cannot be null");
            }

            var existing = await _dataContext.OrphanedFilesCleanupConfigs.FirstOrDefaultAsync();

            if (existing is null)
            {
                existing = new OrphanedFilesCleanupConfig();
                _dataContext.OrphanedFilesCleanupConfigs.Add(existing);
            }

            existing.ExcludePatterns = newConfigDto.ExcludePatterns;
            existing.MinFileAgeMinutes = newConfigDto.MinFileAgeMinutes;
            existing.EmptyAfterXDays = newConfigDto.EmptyAfterXDays;

            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "OrphanedFilesCleanup configuration updated successfully" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save OrphanedFilesCleanup configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
