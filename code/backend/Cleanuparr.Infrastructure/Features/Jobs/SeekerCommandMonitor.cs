using System.Net;
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
    private static readonly TimeSpan AbandonAfter = TimeSpan.FromMinutes(30);

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
        var queueIterator = scope.ServiceProvider.GetRequiredService<IArrQueueIterator>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        List<SeekerCommandTracker> trackers = await eventsContext.SeekerCommandTrackers
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(stoppingToken);

        if (trackers.Count == 0)
        {
            return false;
        }

        Dictionary<Guid, ArrInstance> instancesById = await LoadInstancesAsync(dataContext, trackers, stoppingToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        bool didWork = false;

        List<SeekerCommandTracker> toPoll = [];

        foreach (SeekerCommandTracker tracker in trackers)
        {
            if (IsTerminal(tracker.Status))
            {
                continue;
            }

            if (now - tracker.CreatedAt > CommandTimeout)
            {
                tracker.Status = SearchCommandStatus.TimedOut;
                continue;
            }

            if (!instancesById.ContainsKey(tracker.ArrInstanceId))
            {
                tracker.Status = SearchCommandStatus.Failed;
                continue;
            }

            toPoll.Add(tracker);
        }

        foreach (IGrouping<Guid, SeekerCommandTracker> group in toPoll.GroupBy(t => t.ArrInstanceId))
        {
            ArrInstance arrInstance = instancesById[group.Key];
            IArrClient arrClient = arrClientFactory.GetClient(arrInstance.ArrConfig.Type, arrInstance.Version);
            didWork = true;

            await PollInstanceCommandsAsync(arrClient, arrInstance, group.ToList());
        }

        await eventsContext.SaveChangesAsync(stoppingToken);

        Dictionary<Guid, IReadOnlyList<QueueRecord>> queueSnapshots = await BuildQueueSnapshotsAsync(
            trackers, instancesById, arrClientFactory, queueIterator);

        foreach (SeekerCommandTracker tracker in trackers.Where(t => IsTerminal(t.Status)))
        {
            didWork = true;

            if (await TryPublishOutcomeAsync(tracker, instancesById, queueSnapshots, eventPublisher))
            {
                eventsContext.SeekerCommandTrackers.Remove(tracker);
                continue;
            }

            if (now - tracker.CreatedAt > AbandonAfter)
            {
                _logger.LogWarning(
                    "Abandoning search command {CommandId} for '{Title}' after repeated publish failures (event {EventId})",
                    tracker.CommandId, tracker.ItemTitle, tracker.EventId);
                eventsContext.SeekerCommandTrackers.Remove(tracker);
            }
        }

        try
        {
            await eventsContext.SaveChangesAsync(stoppingToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Search command trackers changed while they were being processed");
        }

        return didWork;
    }

    private async Task PollInstanceCommandsAsync(
        IArrClient arrClient,
        ArrInstance arrInstance,
        List<SeekerCommandTracker> trackers)
    {
        Dictionary<long, ArrCommandStatus>? commands = await TryListCommandsAsync(arrClient, arrInstance);

        if (commands is null)
        {
            foreach (SeekerCommandTracker tracker in trackers)
            {
                await PollSingleCommandAsync(arrClient, arrInstance, tracker);
            }

            return;
        }

        foreach (SeekerCommandTracker tracker in trackers)
        {
            if (commands.TryGetValue(tracker.CommandId, out ArrCommandStatus? status))
            {
                UpdateTrackerStatus(tracker, status);
                continue;
            }

            MarkForgottenCommandAsCompleted(tracker, arrInstance);
        }
    }

    private async Task<Dictionary<long, ArrCommandStatus>?> TryListCommandsAsync(IArrClient arrClient, ArrInstance arrInstance)
    {
        try
        {
            List<ArrCommandStatus> commands = await arrClient.GetCommandsAsync(arrInstance);

            return commands
                .GroupBy(command => command.Id)
                .ToDictionary(group => group.Key, group => group.First());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to list commands on {Instance}, falling back to checking each command individually",
                arrInstance.Name);
            return null;
        }
    }

    private async Task PollSingleCommandAsync(IArrClient arrClient, ArrInstance arrInstance, SeekerCommandTracker tracker)
    {
        try
        {
            ArrCommandStatus status = await arrClient.GetCommandStatusAsync(arrInstance, tracker.CommandId);
            UpdateTrackerStatus(tracker, status);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            MarkForgottenCommandAsCompleted(tracker, arrInstance);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check command {CommandId} status on {Instance}",
                tracker.CommandId, arrInstance.Name);
        }
    }

    private void MarkForgottenCommandAsCompleted(SeekerCommandTracker tracker, ArrInstance arrInstance)
    {
        _logger.LogDebug(
            "Command {CommandId} is no longer known to {Instance}, treating '{Title}' as completed",
            tracker.CommandId, arrInstance.Name, tracker.ItemTitle);
        tracker.Status = SearchCommandStatus.Completed;
    }

    private async Task<Dictionary<Guid, IReadOnlyList<QueueRecord>>> BuildQueueSnapshotsAsync(
        List<SeekerCommandTracker> trackers,
        Dictionary<Guid, ArrInstance> instancesById,
        IArrClientFactory arrClientFactory,
        IArrQueueIterator queueIterator)
    {
        Dictionary<Guid, IReadOnlyList<QueueRecord>> snapshots = [];

        List<Guid> instanceIds = trackers
            .Where(tracker => tracker.Status is SearchCommandStatus.Completed)
            .Select(tracker => tracker.ArrInstanceId)
            .Distinct()
            .Where(instancesById.ContainsKey)
            .ToList();

        foreach (Guid instanceId in instanceIds)
        {
            ArrInstance arrInstance = instancesById[instanceId];
            List<QueueRecord> records = [];

            try
            {
                IArrClient arrClient = arrClientFactory.GetClient(arrInstance.ArrConfig.Type, arrInstance.Version);

                await queueIterator.Iterate(arrClient, arrInstance, pageRecords =>
                {
                    records.AddRange(pageRecords);
                    return Task.CompletedTask;
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inspect the download queue on {Instance}", arrInstance.Name);
            }

            snapshots[instanceId] = records;
        }

        return snapshots;
    }

    private async Task<bool> TryPublishOutcomeAsync(
        SeekerCommandTracker tracker,
        Dictionary<Guid, ArrInstance> instancesById,
        Dictionary<Guid, IReadOnlyList<QueueRecord>> queueSnapshots,
        IEventPublisher eventPublisher)
    {
        try
        {
            if (!instancesById.TryGetValue(tracker.ArrInstanceId, out ArrInstance? arrInstance))
            {
                _logger.LogWarning(
                    "Failing search command {CommandId} for '{Title}': arr instance {ArrInstanceId} no longer exists (event {EventId})",
                    tracker.CommandId, tracker.ItemTitle, tracker.ArrInstanceId, tracker.EventId);
                await eventPublisher.PublishSearchCompleted(tracker.EventId, SearchCommandStatus.Failed, default, string.Empty);
                return true;
            }

            InstanceType instanceType = arrInstance.ArrConfig.Type;
            string instanceUrl = arrInstance.ExternalOrInternalUrl.ToString();

            if (tracker.Status is SearchCommandStatus.Failed or SearchCommandStatus.TimedOut)
            {
                await eventPublisher.PublishSearchCompleted(tracker.EventId, tracker.Status, instanceType, instanceUrl);
                _logger.LogWarning(
                    "Search command {CommandId} for '{Title}' on {Instance} finished with status {Status} (event {EventId})",
                    tracker.CommandId, tracker.ItemTitle, arrInstance.Name, tracker.Status, tracker.EventId);
                return true;
            }

            IReadOnlyList<QueueRecord> queue = queueSnapshots.GetValueOrDefault(tracker.ArrInstanceId, []);
            List<string>? grabbedItems = FindGrabbedItems(tracker, arrInstance, queue);
            await eventPublisher.PublishSearchCompleted(tracker.EventId, SearchCommandStatus.Completed, instanceType, instanceUrl, grabbedItems);
            _logger.LogDebug("Search command completed for event {EventId}", tracker.EventId);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish the outcome of search command {CommandId} for '{Title}' (event {EventId})",
                tracker.CommandId, tracker.ItemTitle, tracker.EventId);
            return false;
        }
    }

    private static bool IsTerminal(SearchCommandStatus status) =>
        status is SearchCommandStatus.Completed or SearchCommandStatus.Failed or SearchCommandStatus.TimedOut;

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
        tracker.Status = commandStatus.Status switch
        {
            ArrCommandState.Completed => SearchCommandStatus.Completed,
            ArrCommandState.Failed => SearchCommandStatus.Failed,
            ArrCommandState.Aborted => SearchCommandStatus.Failed,
            ArrCommandState.Cancelled => SearchCommandStatus.Failed,
            ArrCommandState.Orphaned => SearchCommandStatus.Failed,
            ArrCommandState.Started => SearchCommandStatus.Started,
            _ => tracker.Status
        };
    }

    private List<string>? FindGrabbedItems(
        SeekerCommandTracker tracker,
        ArrInstance arrInstance,
        IReadOnlyList<QueueRecord> queue)
    {
        List<string> grabbedTitles = queue
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
}
