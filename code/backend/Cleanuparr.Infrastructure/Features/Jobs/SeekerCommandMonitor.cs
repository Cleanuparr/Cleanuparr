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
        await Task.Delay(TimeSpan.FromSeconds(10), _timeProvider, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool hadWork = await ProcessPendingCommandsAsync(stoppingToken);
                await Task.Delay(hadWork ? PollInterval : IdleInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SeekerCommandMonitor");
                await Task.Delay(IdleInterval, _timeProvider, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessPendingCommandsAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
        var eventsContext = scope.ServiceProvider.GetRequiredService<EventsContext>();
        var arrClientFactory = scope.ServiceProvider.GetRequiredService<IArrClientFactory>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        List<SeekerCommandTracker> pendingTrackers = await eventsContext.SeekerCommandTrackers
            .Where(t => t.Status != SearchCommandStatus.Completed
                && t.Status != SearchCommandStatus.Failed
                && t.Status != SearchCommandStatus.TimedOut)
            .ToListAsync(stoppingToken);

        if (pendingTrackers.Count == 0)
        {
            return false;
        }

        Dictionary<Guid, ArrInstance> instancesById = await LoadInstancesAsync(dataContext, pendingTrackers, stoppingToken);

        // Handle timed-out commands
        var timedOut = pendingTrackers
            .Where(t => _timeProvider.GetUtcNow() - t.CreatedAt > CommandTimeout)
            .ToList();

        foreach (SeekerCommandTracker tracker in timedOut)
        {
            string instanceName = instancesById.TryGetValue(tracker.ArrInstanceId, out ArrInstance? inst)
                ? inst.Name
                : tracker.ArrInstanceId.ToString();
            _logger.LogWarning("Search command {CommandId} timed out for '{Title}' on {Instance}",
                tracker.CommandId, tracker.ItemTitle, instanceName);
            tracker.Status = SearchCommandStatus.TimedOut;
        }

        // Poll command status for active trackers
        var activeTrackers = pendingTrackers.Except(timedOut).ToList();

        foreach (SeekerCommandTracker tracker in activeTrackers)
        {
            if (!instancesById.TryGetValue(tracker.ArrInstanceId, out ArrInstance? arrInstance))
            {
                continue;
            }

            IArrClient arrClient = arrClientFactory.GetClient(arrInstance.ArrConfig.Type, arrInstance.Version);

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

        await eventsContext.SaveChangesAsync(stoppingToken);

        // Process terminal trackers
        var terminalTrackers = await eventsContext.SeekerCommandTrackers
            .Where(t => t.Status == SearchCommandStatus.Completed
                || t.Status == SearchCommandStatus.Failed
                || t.Status == SearchCommandStatus.TimedOut)
            .ToListAsync(stoppingToken);

        Dictionary<Guid, ArrInstance> terminalInstances = await LoadInstancesAsync(dataContext, terminalTrackers, stoppingToken);

        foreach (SeekerCommandTracker tracker in terminalTrackers)
        {
            if (!terminalInstances.TryGetValue(tracker.ArrInstanceId, out ArrInstance? arrInstance))
            {
                // Instance was deleted; drop the orphaned tracker
                eventsContext.SeekerCommandTrackers.Remove(tracker);
                continue;
            }

            InstanceType instanceType = arrInstance.ArrConfig.Type;
            string instanceUrl = arrInstance.ExternalOrInternalUrl.ToString();

            if (tracker.Status is SearchCommandStatus.Failed or SearchCommandStatus.TimedOut)
            {
                await eventPublisher.PublishSearchCompleted(tracker.EventId, SearchCommandStatus.Failed, instanceType, instanceUrl);
                _logger.LogWarning("Search command failed for event {EventId}", tracker.EventId);
            }
            else
            {
                List<string>? grabbedItems = await InspectDownloadQueueAsync(tracker, arrInstance, arrClientFactory);
                await eventPublisher.PublishSearchCompleted(tracker.EventId, SearchCommandStatus.Completed, instanceType, instanceUrl, grabbedItems);
                _logger.LogDebug("Search command completed for event {EventId}", tracker.EventId);
            }

            eventsContext.SeekerCommandTrackers.Remove(tracker);
        }

        await eventsContext.SaveChangesAsync(stoppingToken);
        return true;
    }

    private static async Task<Dictionary<Guid, ArrInstance>> LoadInstancesAsync(
        DataContext dataContext,
        List<SeekerCommandTracker> trackers,
        CancellationToken stoppingToken)
    {
        List<Guid> ids = trackers.Select(t => t.ArrInstanceId).Distinct().ToList();
        return await dataContext.ArrInstances
            .Include(a => a.ArrConfig)
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, stoppingToken);
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

    private async Task<List<string>?> InspectDownloadQueueAsync(
        SeekerCommandTracker tracker,
        ArrInstance arrInstance,
        IArrClientFactory arrClientFactory)
    {
        try
        {
            IArrClient arrClient = arrClientFactory.GetClient(arrInstance.ArrConfig.Type, arrInstance.Version);

            QueueListResponse queue = await arrClient.GetQueueItemsAsync(arrInstance, 1);

            var grabbedTitles = queue.Records
                .Where(r => arrInstance.ArrConfig.Type == InstanceType.Radarr
                    ? r.MovieId == tracker.ExternalItemId
                    : r.SeriesId == tracker.ExternalItemId
                        && (tracker.SeasonNumber == 0 || r.SeasonNumber == tracker.SeasonNumber))
                .Where(r => !string.IsNullOrEmpty(r.DownloadId))
                .GroupBy(r => r.DownloadId)
                .Select(g => g.First())
                .Select(r => r.Title)
                .ToList();

            if (grabbedTitles.Count > 0)
            {
                _logger.LogInformation("Search for '{Title}' on {Instance} grabbed {Count} items: {Items}",
                    tracker.ItemTitle, arrInstance.Name, grabbedTitles.Count,
                    string.Join(", ", grabbedTitles));
            }

            return grabbedTitles.Count > 0 ? grabbedTitles : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect download queue after search completion");
            return null;
        }
    }
}
