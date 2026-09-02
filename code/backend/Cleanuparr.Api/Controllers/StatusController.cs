using System.Diagnostics;
using Cleanuparr.Api.Features.Status.Contracts.Responses;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatusController : ControllerBase
{
    private readonly ILogger<StatusController> _logger;
    private readonly DataContext _dataContext;
    private readonly IInstanceHealthChecker _healthChecker;

    // Every member is seeded in arr_configs, so a new one must not be forgotten here.
    private static readonly IReadOnlyList<InstanceType> ArrTypes = EnumSentinel.SelectableValues<InstanceType>();

    public StatusController(
        ILogger<StatusController> logger,
        DataContext dataContext,
        IInstanceHealthChecker healthChecker)
    {
        _logger = logger;
        _dataContext = dataContext;
        _healthChecker = healthChecker;
    }

    [HttpGet]
    public async Task<IActionResult> GetSystemStatus()
    {
        using var process = Process.GetCurrentProcess();

        Dictionary<InstanceType, ArrConfig> configsByType = await _dataContext.ArrConfigs
            .Include(x => x.Instances)
            .Where(x => ArrTypes.Contains(x.Type))
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Type);

        Dictionary<string, MediaManagerStatusResponse> mediaManagers = ArrTypes.ToDictionary(
            type => type.ToString(),
            type => new MediaManagerStatusResponse
            {
                InstanceCount = configsByType.TryGetValue(type, out ArrConfig? config) ? config.Instances.Count : 0,
            });

        SystemStatusResponse status = new()
        {
            Application = new ApplicationStatusResponse
            {
                Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown",
                StartTime = process.StartTime,
                UpTime = DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime(),
                MemoryUsageMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2),
                ProcessorTime = process.TotalProcessorTime,
            },
            MediaManagers = mediaManagers,
        };

        return Ok(status);
    }

    [HttpGet("download-client")]
    public async Task<IActionResult> GetDownloadClientStatus()
    {
        List<DownloadClientConfig> downloadClients = await _dataContext.DownloadClients
            .AsNoTracking()
            .ToListAsync();

        List<DownloadClientStatusResponse> clients = downloadClients
            .Select(client => new DownloadClientStatusResponse
            {
                Id = client.Id,
                Name = client.Name,
                Type = client.TypeName,
                Host = client.Host,
                Enabled = client.Enabled,
                IsConnected = client.Enabled,
            })
            .ToList();

        return Ok(new Dictionary<string, List<DownloadClientStatusResponse>> { ["Clients"] = clients });
    }

    [HttpGet("arrs")]
    public async Task<IActionResult> GetMediaManagersStatus()
    {
        Dictionary<string, List<InstanceConnectionResponse>> status = new();

        foreach (InstanceType type in ArrTypes)
        {
            List<ArrInstance> enabledInstances = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .Where(x => x.Type == type)
                .SelectMany(x => x.Instances)
                .Where(x => x.Enabled)
                .AsNoTracking()
                .ToListAsync();

            status[type.ToString()] = await CheckInstancesAsync(type, enabledInstances);
        }

        return Ok(status);
    }

    private async Task<List<InstanceConnectionResponse>> CheckInstancesAsync(InstanceType type, IReadOnlyList<ArrInstance> instances)
    {
        List<InstanceConnectionResponse> results = new(instances.Count);

        foreach (ArrInstance instance in instances)
        {
            try
            {
                await _healthChecker.CheckAsync(type, instance);

                results.Add(new InstanceConnectionResponse
                {
                    Name = instance.Name,
                    Url = instance.Url,
                    IsConnected = true,
                    Message = "Successfully connected",
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "health check failed for {Type} instance | {Url}", type, instance.Url);
                results.Add(new InstanceConnectionResponse
                {
                    Name = instance.Name,
                    Url = instance.Url,
                    IsConnected = false,
                    Message = $"Connection failed: {ex.Message}",
                });
            }
        }

        return results;
    }
}
