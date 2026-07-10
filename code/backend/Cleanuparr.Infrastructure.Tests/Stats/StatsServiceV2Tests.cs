using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Stats;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Providers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Stats;

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

        _service = new StatsService(Substitute.For<ILogger<StatsService>>(), _context, health, jobs, new SqliteDatabaseProvider());
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static AppEvent Event(
        EventType type,
        DeleteReason? deleteReason = null,
        CleanReason? cleanReason = null,
        SearchCommandStatus? searchStatus = null,
        SeekerSearchReason? searchReason = null,
        List<string>? grabbedItems = null,
        bool isDryRun = false,
        DateTimeOffset? timestamp = null) => new()
    {
        EventType = type,
        Message = type.ToString(),
        Severity = EventSeverity.Information,
        Timestamp = timestamp ?? DateTimeOffset.UtcNow.AddHours(-1),
        DeleteReason = deleteReason,
        CleanReason = cleanReason,
        SearchStatus = searchStatus,
        SearchReason = searchReason,
        GrabbedItems = grabbedItems ?? [],
        IsDryRun = isDryRun,
    };

    [Fact]
    public async Task GetStatsV2Async_DerivesTimeframeMetricsFromEvents()
    {
        _context.Events.Add(Event(EventType.StalledStrike));
        _context.Events.Add(Event(EventType.StalledStrike));
        _context.Events.Add(Event(EventType.FailedImportStrike));
        _context.Events.Add(Event(EventType.StrikeReset));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.AllFilesBlocked));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled));
        await _context.SaveChangesAsync();

        StatsV2Response stats = await _service.GetStatsV2Async(24);

        stats.TimeframeHours.ShouldBe(24);
        stats.Events.Total.ShouldBe(6);
        stats.Events.ByType["StalledStrike"].ShouldBe(2);

        stats.Strikes.Total.ShouldBe(3);
        stats.Strikes.ByType["Stalled"].ShouldBe(2);
        stats.Strikes.ByType["FailedImport"].ShouldBe(1);
        stats.Strikes.Total.ShouldBe(stats.Strikes.ByType.Values.Sum());
        stats.Strikes.Recovered.ShouldBe(1);

        stats.Removals.Total.ShouldBe(2);
        stats.Removals.ByReason["AllFilesBlocked"].ShouldBe(1);
        stats.Removals.ByReason["Stalled"].ShouldBe(1);
    }

    [Fact]
    public async Task GetStatsV2Async_MalwareIsDerivedFromRemovalReasons()
    {
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.AllFilesBlocked));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.AtLeastOneFileBlocked));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.SlowSpeed));
        await _context.SaveChangesAsync();

        StatsV2Response stats = await _service.GetStatsV2Async(24);

        int malware = stats.Removals.ByReason.GetValueOrDefault("AllFilesBlocked")
            + stats.Removals.ByReason.GetValueOrDefault("AtLeastOneFileBlocked");
        malware.ShouldBe(2);
        stats.Removals.Total.ShouldBe(3);
    }

    [Fact]
    public async Task GetStatsV2Async_StrikesRespectTimeframe()
    {
        _context.Events.Add(Event(EventType.StalledStrike));
        _context.Events.Add(Event(EventType.StalledStrike, timestamp: DateTimeOffset.UtcNow.AddHours(-100)));
        await _context.SaveChangesAsync();

        StatsV2Response stats = await _service.GetStatsV2Async(24);

        stats.Strikes.Total.ShouldBe(1);
        stats.Strikes.ByType["Stalled"].ShouldBe(1);
    }

    [Fact]
    public async Task GetStatsV2Async_ExcludesDryRunByDefault()
    {
        _context.Events.Add(Event(EventType.StalledStrike));
        _context.Events.Add(Event(EventType.StalledStrike, isDryRun: true));
        await _context.SaveChangesAsync();

        StatsV2Response live = await _service.GetStatsV2Async(24);
        live.Strikes.Total.ShouldBe(1);
        live.Events.ByType["StalledStrike"].ShouldBe(1);

        StatsV2Response withDryRun = await _service.GetStatsV2Async(24, includeDryRun: true);
        withDryRun.Strikes.Total.ShouldBe(2);
        withDryRun.Events.ByType["StalledStrike"].ShouldBe(2);
    }

    [Fact]
    public async Task GetStatsV2Async_CleanedGroupsByReasonSkippingNone()
    {
        _context.Events.Add(Event(EventType.DownloadCleaned, cleanReason: CleanReason.MaxRatioReached));
        _context.Events.Add(Event(EventType.DownloadCleaned, cleanReason: CleanReason.MaxRatioReached));
        _context.Events.Add(Event(EventType.DownloadCleaned, cleanReason: CleanReason.MaxSeedTimeReached));
        _context.Events.Add(Event(EventType.DownloadCleaned, cleanReason: CleanReason.None));
        await _context.SaveChangesAsync();

        StatsV2Response stats = await _service.GetStatsV2Async(24);

        stats.Cleaned.Total.ShouldBe(4);
        stats.Cleaned.ByReason["MaxRatioReached"].ShouldBe(2);
        stats.Cleaned.ByReason["MaxSeedTimeReached"].ShouldBe(1);
        stats.Cleaned.ByReason.ShouldNotContainKey("None");
    }

    [Fact]
    public async Task GetStatsV2Async_SearchesAggregateStatusReasonAndGrabbed()
    {
        _context.Events.Add(Event(EventType.SearchTriggered, searchStatus: SearchCommandStatus.Completed,
            searchReason: SeekerSearchReason.Missing, grabbedItems: ["a", "b"]));
        _context.Events.Add(Event(EventType.SearchTriggered, searchStatus: SearchCommandStatus.Completed,
            searchReason: SeekerSearchReason.QualityCutoffNotMet, grabbedItems: ["c"]));
        _context.Events.Add(Event(EventType.SearchTriggered, searchStatus: SearchCommandStatus.Failed,
            searchReason: SeekerSearchReason.Missing));
        _context.Events.Add(Event(EventType.SearchTriggered, searchStatus: SearchCommandStatus.TimedOut,
            searchReason: SeekerSearchReason.Replacement));
        _context.Events.Add(Event(EventType.SearchTriggered, searchStatus: SearchCommandStatus.Pending,
            searchReason: SeekerSearchReason.Missing));
        await _context.SaveChangesAsync();

        StatsV2Response stats = await _service.GetStatsV2Async(24);

        stats.Searches.Total.ShouldBe(5);
        stats.Searches.Completed.ShouldBe(2);
        stats.Searches.Failed.ShouldBe(2);
        stats.Searches.Grabbed.ShouldBe(3);
        stats.Searches.ByReason["Missing"].ShouldBe(3);
        stats.Searches.ByReason["QualityCutoffNotMet"].ShouldBe(1);
        stats.Searches.ByReason["Replacement"].ShouldBe(1);
    }

    [Fact]
    public async Task GetTimelineAsync_FiltersByMetricAndDryRun()
    {
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.AllFilesBlocked));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled));
        _context.Events.Add(Event(EventType.StrikeReset));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.SlowSpeed, isDryRun: true));
        await _context.SaveChangesAsync();

        List<TimelineBucketDto> removed = await _service.GetTimelineAsync("removed", 24);
        removed.Sum(b => b.Count).ShouldBe(2);

        List<TimelineBucketDto> removedWithDryRun = await _service.GetTimelineAsync("removed", 24, includeDryRun: true);
        removedWithDryRun.Sum(b => b.Count).ShouldBe(3);

        List<TimelineBucketDto> malware = await _service.GetTimelineAsync("malwareBlocked", 24);
        malware.Sum(b => b.Count).ShouldBe(1);
    }

    [Fact]
    public async Task GetTimelineAsync_MonthBucketsAreFirstOfMonth()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled, timestamp: now));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled, timestamp: now.AddDays(-40)));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled, timestamp: now.AddDays(-75)));
        await _context.SaveChangesAsync();

        List<TimelineBucketDto> series = await _service.GetTimelineAsync("removed", 8760, TimelineBucketSize.Month);

        series.Sum(b => b.Count).ShouldBe(3);
        series.Count(b => b.Count > 0).ShouldBe(3);
        series.ShouldAllBe(b => b.Date.Day == 1);
    }

    [Fact]
    public async Task GetTimelineAsync_WeekBucketsStartOnMonday()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled, timestamp: now));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled, timestamp: now.AddDays(-10)));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled, timestamp: now.AddDays(-20)));
        await _context.SaveChangesAsync();

        List<TimelineBucketDto> series = await _service.GetTimelineAsync("removed", 720, TimelineBucketSize.Week);

        series.Sum(b => b.Count).ShouldBe(3);
        series.Count(b => b.Count > 0).ShouldBe(3);
        series.ShouldAllBe(b => b.Date.DayOfWeek == DayOfWeek.Monday);
    }
}
