using System.Diagnostics;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence;
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
    private readonly IArrClientFactory _arrClientFactory;

    private static readonly IReadOnlyList<InstanceType> ArrTypes =
    [
        InstanceType.Sonarr,
        InstanceType.Radarr,
        InstanceType.Lidarr,
        InstanceType.Readarr,
        InstanceType.Whisparr,
        InstanceType.LazyLibrarian,
    ];

    public StatusController(
        ILogger<StatusController> logger,
        DataContext dataContext,
        IArrClientFactory arrClientFactory)
    {
        _logger = logger;
        _dataContext = dataContext;
        _arrClientFactory = arrClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetSystemStatus()
    {
        var process = Process.GetCurrentProcess();

        var configsByType = await _dataContext.ArrConfigs
            .Include(x => x.Instances)
            .Where(x => ArrTypes.Contains(x.Type))
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Type);

        var mediaManagers = ArrTypes.ToDictionary(
            type => type.ToString(),
            type => (object)new { InstanceCount = configsByType.TryGetValue(type, out var c) ? c.Instances.Count : 0 });

        var status = new
        {
            Application = new
            {
                Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown",
                process.StartTime,
                UpTime = DateTime.Now - process.StartTime,
                MemoryUsageMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2),
                ProcessorTime = process.TotalProcessorTime
            },
            DownloadClient = new
            {
                // TODO
            },
            MediaManagers = mediaManagers,
        };

        return Ok(status);
    }

    [HttpGet("download-client")]
    public async Task<IActionResult> GetDownloadClientStatus()
    {
        var downloadClients = await _dataContext.DownloadClients
            .AsNoTracking()
            .ToListAsync();
        var result = new Dictionary<string, object>();

        if (downloadClients.Count > 0)
        {
            var clientsStatus = new List<object>();
            foreach (var client in downloadClients)
            {
                clientsStatus.Add(new
                {
                    client.Id,
                    client.Name,
                    Type = client.TypeName,
                    client.Host,
                    client.Enabled,
                    IsConnected = client.Enabled, // We can't check connection status without implementing test methods
                });
            }

            result["Clients"] = clientsStatus;
        }

        return Ok(result);
    }

    [HttpGet("arrs")]
    public async Task<IActionResult> GetMediaManagersStatus()
    {
        var status = new Dictionary<string, object>();

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

    private async Task<List<object>> CheckInstancesAsync(InstanceType type, IReadOnlyList<ArrInstance> instances)
    {
        var results = new List<object>(instances.Count);

        foreach (ArrInstance instance in instances)
        {
            try
            {
                var client = _arrClientFactory.GetClient(type, instance.Version);
                await client.HealthCheckAsync(instance);

                results.Add(new
                {
                    instance.Name,
                    instance.Url,
                    IsConnected = true,
                    Message = "Successfully connected"
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    instance.Name,
                    instance.Url,
                    IsConnected = false,
                    Message = $"Connection failed: {ex.Message}"
                });
            }
        }

        return results;
    }
}
