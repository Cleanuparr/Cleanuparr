using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Events;
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

            GeneralConfig config = await dataContext.GeneralConfigs
                .AsNoTracking()
                .FirstAsync();

            DateTimeOffset eventCutoff = DateTimeOffset.UtcNow.AddDays(-_eventRetentionDays);

            // Resolved manual events are transient
            await DeleteResolvedManualEventsAsync(eventsContext, eventCutoff);

            // Prune events older than the configured retention window
            await PruneEventsAsync(eventsContext, config.HistoryRetentionDays);

            await CleanupStrikesAsync(eventsContext, config.StrikeInactivityWindowHours);

            // Prune old job runs no longer referenced by any active strike or event
            await PruneJobRunsAsync(eventsContext, eventCutoff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform event cleanup");
        }
    }

    internal async Task DeleteResolvedManualEventsAsync(EventsContext eventsContext, DateTimeOffset cutoff)
    {
        int deleted = await eventsContext.ManualEvents
            .Where(e => e.IsResolved)
            .Where(e => (e.ResolvedAt ?? e.Timestamp) < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogInformation("Deleted {count} resolved manual events older than {days} days", deleted, _eventRetentionDays);
        }
    }

    internal async Task PruneEventsAsync(EventsContext eventsContext, ushort retentionDays)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        int deleted = await eventsContext.Events
            .Where(e => e.Timestamp < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogInformation("Pruned {count} events older than {days} days", deleted, retentionDays);
        }
    }

    internal async Task PruneJobRunsAsync(EventsContext eventsContext, DateTimeOffset cutoff)
    {
        int deleted = await eventsContext.JobRuns
            .Where(j => j.CompletedAt != null && j.StartedAt < cutoff)
            .Where(j => !eventsContext.Strikes.Any(s => s.JobRunId == j.Id))
            .Where(j => !eventsContext.Events.Any(e => e.JobRunId == j.Id))
            .Where(j => !eventsContext.ManualEvents.Any(m => m.JobRunId == j.Id))
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogInformation("Pruned {count} unreferenced job runs", deleted);
        }
    }

    private async Task CleanupStrikesAsync(EventsContext eventsContext, ushort inactivityWindowHours)
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddHours(-inactivityWindowHours);

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