using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Events;

/// <summary>
/// Background service that periodically cleans up old events
/// </summary>
public class EventCleanupService : BackgroundService
{
    private readonly ILogger<EventCleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(4); // Run every 4 hours
    private readonly int _eventRetentionDays = 30; // Keep events for 30 days

    public EventCleanupService(ILogger<EventCleanupService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event cleanup service started. Interval: {interval}, Retention: {retention} days", 
            _cleanupInterval, _eventRetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await PerformCleanupAsync();
                
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during event cleanup");
            }
        }

        _logger.LogInformation("Event cleanup service stopped");
    }

    private async Task PerformCleanupAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var eventsContext = scope.ServiceProvider.GetRequiredService<EventsContext>();
            var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var cutoffDate = DateTime.UtcNow.AddDays(-_eventRetentionDays);
            await eventsContext.Events
                .Where(e => e.Timestamp < cutoffDate)
                .ExecuteDeleteAsync();
            await eventsContext.ManualEvents
                .Where(e => e.Timestamp < cutoffDate)
                .Where(e => e.IsResolved)
                .ExecuteDeleteAsync();

            await CleanupStrikesAsync(eventsContext, dataContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform event cleanup");
        }
    }

    private async Task CleanupStrikesAsync(EventsContext eventsContext, DataContext dataContext)
    {
        var config = await dataContext.GeneralConfigs
            .AsNoTracking()
            .FirstAsync();

        var inactivityWindowHours = config.StrikeInactivityWindowHours;
        var cutoffDate = DateTime.UtcNow.AddHours(-inactivityWindowHours);

        // Sliding window: find items whose most recent strike is older than the inactivity window.
        // As long as a download keeps receiving new strikes, all its strikes are preserved.
        var inactiveItemIds = await eventsContext.Strikes
            .GroupBy(s => s.DownloadItemId)
            .Where(g => g.Max(s => s.CreatedAt) < cutoffDate)
            .Select(g => g.Key)
            .ToListAsync();

        if (inactiveItemIds.Count > 0)
        {
            var deletedStrikesCount = await eventsContext.Strikes
                .Where(s => inactiveItemIds.Contains(s.DownloadItemId))
                .ExecuteDeleteAsync();

            if (deletedStrikesCount > 0)
            {
                _logger.LogInformation(
                    "Cleaned up {count} strikes from {items} inactive items (no new strikes for {hours} hours)",
                    deletedStrikesCount, inactiveItemIds.Count, inactivityWindowHours);
            }
        }

        // Clean up orphaned DownloadItems (those with no strikes)
        int deletedDownloadItemsCount = await eventsContext.DownloadItems
            .Where(d => !d.Strikes.Any())
            .ExecuteDeleteAsync();

        if (deletedDownloadItemsCount > 0)
        {
            _logger.LogTrace("Cleaned up {count} download items with 0 strikes", deletedDownloadItemsCount);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Event cleanup service stopping...");
        await base.StopAsync(cancellationToken);
    }
} 