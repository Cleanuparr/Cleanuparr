using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Stats;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Stats;

/// <summary>
/// Verifies the v2 stats service derives strike/event/malware metrics from active events + archived history.
/// </summary>
public class StatsServiceV2Tests : IDisposable
{
    private readonly EventsContext _context;
    private readonly StatsService _service;

    public StatsServiceV2Tests()
    {
        _context = TestEventsContextFactory.Create();

        IHealthCheckService health = Substitute.For<IHealthCheckService>();
        health.GetAllClientHealth().Returns(new Dictionary<Guid, HealthStatus>());
        health.GetAllArrInstanceHealth().Returns(new Dictionary<Guid, ArrHealthStatus>());

        IJobManagementService jobs = Substitute.For<IJobManagementService>();
        jobs.GetAllJobs().ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<JobInfo>>([]));

        _service = new StatsService(Substitute.For<ILogger<StatsService>>(), _context, health, jobs);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private AppEvent Event(EventType type, DeleteReason? deleteReason = null) => new()
    {
        EventType = type,
        Message = type.ToString(),
        Severity = EventSeverity.Information,
        Timestamp = DateTimeOffset.UtcNow.AddHours(-1),
        DeleteReason = deleteReason,
    };

    [Fact]
    public async Task GetStatsV2Async_DerivesMetricsFromActiveAndHistory()
    {
        // Active events
        _context.Events.Add(Event(EventType.StalledStrike));
        _context.Events.Add(Event(EventType.StrikeReset));
        _context.Events.Add(Event(EventType.QueueItemDeleted, DeleteReason.AllFilesBlocked));
        _context.Events.Add(Event(EventType.QueueItemDeleted, DeleteReason.Stalled));

        // Archived history (counts too)
        _context.EventHistory.Add(new EventHistory
        {
            Id = Guid.NewGuid(),
            EventType = EventType.StalledStrike,
            Message = "archived strike",
            Severity = EventSeverity.Important,
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2),
            ArchivedAt = DateTimeOffset.UtcNow,
        });

        // A live active strike
        DownloadItem item = new() { DownloadId = "h1", Title = "t1" };
        JobRun run = new() { Id = Guid.NewGuid(), Type = JobType.QueueCleaner };
        _context.DownloadItems.Add(item);
        _context.JobRuns.Add(run);
        _context.Strikes.Add(new Strike { DownloadItemId = item.Id, JobRunId = run.Id, Type = StrikeType.Stalled });

        await _context.SaveChangesAsync();

        StatsV2Response stats = await _service.GetStatsV2Async(24, "24h");

        stats.Strikes.Issued.ShouldBe(2);      // 1 active + 1 history StalledStrike
        stats.Strikes.Recovered.ShouldBe(1);   // StrikeReset
        stats.Strikes.Removed.ShouldBe(2);     // two QueueItemDeleted
        stats.Malware.Blocked.ShouldBe(1);     // only AllFilesBlocked
        stats.Strikes.Active["Stalled"].ShouldBe(1);
        stats.Events.ByType["StalledStrike"].ShouldBe(2); // merged active + history
        stats.Events.TotalCount.ShouldBe(5);
        stats.Window.ShouldBe("24h");
    }

    [Fact]
    public async Task GetTimelineAsync_BucketsMatchingEventsByDay()
    {
        _context.Events.Add(Event(EventType.QueueItemDeleted, DeleteReason.AllFilesBlocked));
        _context.Events.Add(Event(EventType.QueueItemDeleted, DeleteReason.Stalled));
        _context.Events.Add(Event(EventType.StrikeReset)); // not a removal
        _context.EventHistory.Add(new EventHistory
        {
            Id = Guid.NewGuid(),
            EventType = EventType.QueueItemDeleted,
            Message = "archived removal",
            Severity = EventSeverity.Important,
            Timestamp = DateTimeOffset.UtcNow.AddHours(-3),
            ArchivedAt = DateTimeOffset.UtcNow,
            DeleteReason = DeleteReason.SlowSpeed,
        });
        await _context.SaveChangesAsync();

        List<TimelineBucketDto> removed = await _service.GetTimelineAsync("removed", 24);
        removed.Sum(b => b.Count).ShouldBe(3); // 2 active + 1 history QueueItemDeleted

        List<TimelineBucketDto> malware = await _service.GetTimelineAsync("malwareBlocked", 24);
        malware.Sum(b => b.Count).ShouldBe(1); // only AllFilesBlocked
    }
}
