using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Jobs;

/// <summary>
/// Background service that polls arr command status for pending search commands
/// and inspects the download queue for grabbed items after completion.
/// </summary>
public class SeekerCommandMonitor : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

    private readonly ILogger<SeekerCommandMonitor> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public SeekerCommandMonitor(
        ILogger<SeekerCommandMonitor> logger,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app startup
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool hadWork = await ProcessPendingCommandsAsync(stoppingToken);
                await Task.Delay(hadWork ? PollInterval : IdleInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SeekerCommandMonitor");
                await Task.Delay(IdleInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessPendingCommandsAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        var arrClientFactory = scope.ServiceProvider.GetRequiredService<IArrClientFactory>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        List<SeekerCommandTracker> pendingTrackers = await dataContext.SeekerCommandTrackers
            .Include(t => t.ArrInstance)
                .ThenInclude(a => a.ArrConfig)
            .Where(t => t.Status != SearchCommandStatus.Completed
                && t.Status != SearchCommandStatus.Failed
                && t.Status != SearchCommandStatus.TimedOut)
            .ToListAsync(stoppingToken);

        if (pendingTrackers.Count == 0)
        {
            return false;
        }

        // Handle timed-out commands
        var timedOut = pendingTrackers
            .Where(t => _timeProvider.GetUtcNow().UtcDateTime - t.CreatedAt > CommandTimeout)
            .ToList();

        foreach (var tracker in timedOut)
        {
            _logger.LogWarning("Search command {CommandId} timed out for '{Title}' on {Instance}",
                tracker.CommandId, tracker.ItemTitle, tracker.ArrInstance.Name);
            tracker.Status = SearchCommandStatus.TimedOut;
        }

        // Group remaining by event ID for batch processing
        var activeTrackers = pendingTrackers.Except(timedOut).ToList();
        var trackersByInstance = activeTrackers.GroupBy(t => t.ArrInstanceId);

        foreach (var instanceGroup in trackersByInstance)
        {
            var arrInstance = instanceGroup.First().ArrInstance;
            IArrClient arrClient = arrClientFactory.GetClient(arrInstance.ArrConfig.Type, arrInstance.Version);

            foreach (var tracker in instanceGroup)
            {
                try
                {
                    ArrCommandStatus status = await arrClient.GetCommandStatusAsync(arrInstance, tracker.CommandId);
                    UpdateTrackerStatus(tracker, status);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check command {CommandId} status on {Instance}",
                        tracker.CommandId, arrInstance.Name);
                }
            }
        }

        await dataContext.SaveChangesAsync(stoppingToken);

        // Process completed/failed events
        var allTrackers = await dataContext.SeekerCommandTrackers
            .Include(t => t.ArrInstance)
                .ThenInclude(a => a.ArrConfig)
            .ToListAsync(stoppingToken);

        var trackersByEvent = allTrackers.GroupBy(t => t.EventId);

        foreach (var eventGroup in trackersByEvent)
        {
            Guid eventId = eventGroup.Key;
            var trackers = eventGroup.ToList();

            bool allTerminal = trackers.All(t =>
                t.Status is SearchCommandStatus.Completed
                    or SearchCommandStatus.Failed
                    or SearchCommandStatus.TimedOut);

            if (!allTerminal)
            {
                continue;
            }

            bool anyFailed = trackers.Any(t => t.Status is SearchCommandStatus.Failed or SearchCommandStatus.TimedOut);

            if (anyFailed)
            {
                await eventPublisher.PublishSearchCompleted(eventId, SearchCommandStatus.Failed);
                _logger.LogWarning("Search command(s) failed for event {EventId}", eventId);
            }
            else
            {
                // All completed — inspect download queue for grabbed items
                object? resultData = await InspectDownloadQueueAsync(trackers, arrClientFactory);
                await eventPublisher.PublishSearchCompleted(eventId, SearchCommandStatus.Completed, resultData);
                _logger.LogDebug("Search command(s) completed for event {EventId}", eventId);
            }

            // Remove processed trackers
            dataContext.SeekerCommandTrackers.RemoveRange(trackers);
        }

        await dataContext.SaveChangesAsync(stoppingToken);
        return true;
    }

    private static void UpdateTrackerStatus(SeekerCommandTracker tracker, ArrCommandStatus commandStatus)
    {
        tracker.Status = commandStatus.Status.ToLowerInvariant() switch
        {
            "completed" => SearchCommandStatus.Completed,
            "failed" => SearchCommandStatus.Failed,
            "started" => SearchCommandStatus.Started,
            _ => tracker.Status // Keep current status for queued/other states
        };
    }

    private async Task<object?> InspectDownloadQueueAsync(
        List<SeekerCommandTracker> trackers,
        IArrClientFactory arrClientFactory)
    {
        var allGrabbedItems = new List<object>();

        // Group by instance to inspect each instance's queue separately
        foreach (var instanceGroup in trackers.GroupBy(t => t.ArrInstanceId))
        {
            try
            {
                var tracker = instanceGroup.First();
                var arrInstance = tracker.ArrInstance;
                IArrClient arrClient = arrClientFactory.GetClient(arrInstance.ArrConfig.Type, arrInstance.Version);

                // Fetch the first page of the queue
                QueueListResponse queue = await arrClient.GetQueueItemsAsync(arrInstance, 1);

                // Find records matching any tracker in this instance group
                foreach (var t in instanceGroup)
                {
                    var grabbedItems = queue.Records
                        .Where(r => t.ItemType == InstanceType.Radarr
                            ? r.MovieId == t.ExternalItemId
                            : r.SeriesId == t.ExternalItemId
                                && (t.SeasonNumber == 0 || r.SeasonNumber == t.SeasonNumber))
                        .Select(r => new
                        {
                            r.Title,
                            r.Status,
                            r.Protocol,
                        })
                        .ToList();

                    if (grabbedItems.Count > 0)
                    {
                        _logger.LogInformation("Search for '{Title}' on {Instance} grabbed {Count} items: {Items}",
                            t.ItemTitle, arrInstance.Name, grabbedItems.Count,
                            string.Join(", ", grabbedItems.Select(g => g.Title)));

                        allGrabbedItems.AddRange(grabbedItems);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inspect download queue after search completion");
            }
        }

        return allGrabbedItems.Count > 0 ? new { GrabbedItems = allGrabbedItems } : null;
    }
}
