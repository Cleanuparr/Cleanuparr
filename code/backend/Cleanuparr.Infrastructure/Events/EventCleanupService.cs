using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

            // Move events past the hot window into history instead of deleting them
            await ArchiveExpiredEventsAsync(eventsContext, eventCutoff);

            // Resolved manual events are transient and are not archived
            await DeleteResolvedManualEventsAsync(eventsContext, eventCutoff);

            // Prune cold history older than the configured retention window
            await PruneEventHistoryAsync(eventsContext, config.HistoryRetentionDays);

            await CleanupStrikesAsync(eventsContext, config.StrikeInactivityWindowHours);

            // Prune old job runs no longer referenced by any active strike or event
            await PruneJobRunsAsync(eventsContext, eventCutoff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform event cleanup");
        }
    }

    /// <summary>
    /// Moves events older than the hot window into <see cref="EventHistory"/> (preserving their Id), in batches.
    /// </summary>
    internal async Task ArchiveExpiredEventsAsync(EventsContext eventsContext, DateTimeOffset cutoff)
    {
        const int batchSize = 5000;
        int totalArchived = 0;

        while (true)
        {
            List<AppEvent> batch = await eventsContext.Events
                .AsNoTracking()
                .Where(e => e.Timestamp < cutoff)
                .OrderBy(e => e.Timestamp)
                .Take(batchSize)
                .ToListAsync();

            if (batch.Count is 0)
            {
                break;
            }

            DateTimeOffset archivedAt = DateTimeOffset.UtcNow;
            List<EventHistory> history = batch
                .Select(e => new EventHistory
                {
                    Id = e.Id,
                    Timestamp = e.Timestamp,
                    EventType = e.EventType,
                    Message = e.Message,
                    Severity = e.Severity,
                    ArchivedAt = archivedAt,
                    TrackingId = e.TrackingId,
                    StrikeId = e.StrikeId,
                    JobRunId = e.JobRunId,
                    ArrInstanceId = e.ArrInstanceId,
                    DownloadClientId = e.DownloadClientId,
                    SearchStatus = e.SearchStatus,
                    CompletedAt = e.CompletedAt,
                    CycleId = e.CycleId,
                    IsDryRun = e.IsDryRun,
                    ItemTitle = e.ItemTitle,
                    ItemHash = e.ItemHash,
                    StrikeCount = e.StrikeCount,
                    FailedImportReasons = e.FailedImportReasons,
                    DeleteReason = e.DeleteReason,
                    RemoveFromClient = e.RemoveFromClient,
                    CleanReason = e.CleanReason,
                    CleanedCategory = e.CleanedCategory,
                    SeedRatio = e.SeedRatio,
                    SeedingTimeHours = e.SeedingTimeHours,
                    OldCategory = e.OldCategory,
                    NewCategory = e.NewCategory,
                    IsCategoryTag = e.IsCategoryTag,
                    SearchType = e.SearchType,
                    SearchReason = e.SearchReason,
                    GrabbedItems = e.GrabbedItems,
                })
                .ToList();

            // Insert history and delete the source rows atomically. Without this, a failure
            // between the two writes leaves the history rows committed, so the next run re-archives
            // the same events and their preserved Ids collide on the EventHistory primary key.
            await using IDbContextTransaction transaction = await eventsContext.Database.BeginTransactionAsync();

            eventsContext.EventHistory.AddRange(history);
            await eventsContext.SaveChangesAsync();

            List<Guid> ids = batch.Select(e => e.Id).ToList();
            await eventsContext.Events
                .Where(e => ids.Contains(e.Id))
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();

            totalArchived += batch.Count;

            if (batch.Count < batchSize)
            {
                break;
            }
        }

        if (totalArchived > 0)
        {
            _logger.LogInformation("Archived {count} events older than {days} days to history", totalArchived, _eventRetentionDays);
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

    internal async Task PruneEventHistoryAsync(EventsContext eventsContext, ushort historyRetentionDays)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-historyRetentionDays);
        int deleted = await eventsContext.EventHistory
            .Where(h => h.ArchivedAt < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogInformation("Pruned {count} event history entries older than {days} days", deleted, historyRetentionDays);
        }
    }

    internal async Task PruneJobRunsAsync(EventsContext eventsContext, DateTimeOffset cutoff)
    {
        int deleted = await eventsContext.JobRuns
            .Where(j => j.CompletedAt != null && j.StartedAt < cutoff)
            .Where(j => !eventsContext.Strikes.Any(s => s.JobRunId == j.Id))
            .Where(j => !eventsContext.Events.Any(e => e.JobRunId == j.Id))
            .Where(j => !eventsContext.ManualEvents.Any(m => m.JobRunId == j.Id))
            .Where(j => !eventsContext.EventHistory.Any(h => h.JobRunId == j.Id))
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