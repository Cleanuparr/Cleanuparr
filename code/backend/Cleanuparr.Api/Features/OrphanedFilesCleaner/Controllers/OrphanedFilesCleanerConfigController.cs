using System.ComponentModel.DataAnnotations;

using Cleanuparr.Api.Features.OrphanedFilesCleaner.Contracts.Requests;
using Cleanuparr.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.OrphanedFilesCleaner.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize]
public sealed class OrphanedFilesCleanerConfigController : ControllerBase
{
    private readonly ILogger<OrphanedFilesCleanerConfigController> _logger;
    private readonly DataContext _dataContext;

    public OrphanedFilesCleanerConfigController(
        ILogger<OrphanedFilesCleanerConfigController> logger,
        DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    [HttpGet("orphaned_files_cleaner")]
    public async Task<IActionResult> GetOrphanedFilesCleanerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.OrphanedFilesCleanerConfigs
                .AsNoTracking()
                .FirstAsync();

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

    [HttpPut("orphaned_files_cleaner")]
    public async Task<IActionResult> UpdateOrphanedFilesCleanerConfig([FromBody] UpdateOrphanedFilesCleanerConfigRequest newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            if (newConfigDto is null)
            {
                throw new ValidationException("Request body cannot be null");
            }

            var oldConfig = await _dataContext.OrphanedFilesCleanerConfigs.FirstAsync();

            oldConfig.ExcludePatterns = newConfigDto.ExcludePatterns;
            oldConfig.MinFileAgeMinutes = newConfigDto.MinFileAgeMinutes;
            oldConfig.EmptyAfterXDays = newConfigDto.EmptyAfterXDays;

            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "OrphanedFilesCleaner configuration updated successfully" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save OrphanedFilesCleaner configuration");
            throw;
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
