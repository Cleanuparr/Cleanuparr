using System.Text.Json;
using System.Text.Json.Serialization;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Shared.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public sealed class AppStatusRefreshService : BackgroundService
{
    private static readonly Uri StatusUri = new("https://cleanuparr-status.pages.dev/status.json");
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppStatusSnapshot _snapshot;
    private readonly IHubContext<AppHub> _hubContext;
    private readonly ILogger<AppStatusRefreshService> _logger;

    private AppStatus? _lastBroadcast;

    public AppStatusRefreshService(
        IHttpClientFactory httpClientFactory,
        AppStatusSnapshot snapshot,
        IHubContext<AppHub> hubContext,
        ILogger<AppStatusRefreshService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _snapshot = snapshot;
        _hubContext = hubContext;
        _logger = logger;
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
            client.Timeout = RequestTimeout;

            using var response = await client.GetAsync(StatusUri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<StatusPayload>(stream, cancellationToken: cancellationToken);
                var latest = payload?.Version;

                if (_snapshot.UpdateLatestVersion(latest, out var status))
                {
                    await BroadcastAsync(status, cancellationToken);
                }
            }
            else
            {
                _logger.LogWarning("Failed to fetch app status: {StatusCode}", response.StatusCode);
                if (_snapshot.UpdateLatestVersion(null, out var status))
                {
                    await BroadcastAsync(status, cancellationToken);
                }
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
                await BroadcastAsync(status, CancellationToken.None);
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

    private sealed record StatusPayload([property: JsonPropertyName("version")] string? Version);
}
