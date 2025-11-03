using System.Text.Json;
using Cleanuparr.Domain.Entities.AppStatus;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Shared.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cleanuparr.Infrastructure.Services;

public sealed class AppStatusRefreshService : BackgroundService
{
    private readonly ILogger<AppStatusRefreshService> _logger;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppStatusSnapshot _snapshot;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppStatus? _lastBroadcast;
    
    private static readonly Uri StatusUri = new("https://cleanuparr-status.pages.dev/status.json");
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    
    public AppStatusRefreshService(
        ILogger<AppStatusRefreshService> logger,
        IHubContext<AppHub> hubContext,
        IHttpClientFactory httpClientFactory,
        AppStatusSnapshot snapshot,
        JsonSerializerOptions jsonOptions
    )
    {
        _logger = logger;
        _hubContext = hubContext;
        _httpClientFactory = httpClientFactory;
        _snapshot = snapshot;
        _jsonOptions = jsonOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAsync(stoppingToken);

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(Constants.HttpClientWithRetryName);

            using var response = await client.GetAsync(StatusUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<Status>(stream, _jsonOptions, cancellationToken: cancellationToken);
            var latest = payload?.Version;

            if (_snapshot.UpdateLatestVersion(latest, out var status))
            {
                await BroadcastAsync(status, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh app status");
            if (_snapshot.UpdateLatestVersion(null, out var status))
            {
                await BroadcastAsync(status, cancellationToken);
            }
        }
    }

    private async Task BroadcastAsync(AppStatus status, CancellationToken cancellationToken)
    {
        if (status.Equals(_lastBroadcast))
        {
            return;
        }

        await _hubContext.Clients.All.SendAsync("AppStatusUpdated", status, cancellationToken);
        _lastBroadcast = status;
    }
}
