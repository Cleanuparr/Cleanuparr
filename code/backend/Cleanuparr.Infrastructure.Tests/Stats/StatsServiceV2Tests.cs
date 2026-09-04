using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Infrastructure.Stats;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Models.State;
using Cleanuparr.Persistence.Providers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Stats;

public class StatsServiceV2Tests : IDisposable
{
    private readonly EventsContext _context;
    private readonly IHealthCheckService _health;
    private readonly IJobManagementService _jobs;
    private readonly StatsService _service;

    public StatsServiceV2Tests()
    {
        _context = TestEventsContextFactory.Create();

        _health = Substitute.For<IHealthCheckService>();
        _health.GetAllClientHealth().Returns(new Dictionary<Guid, HealthStatus>());
        _health.GetAllArrInstanceHealth().Returns(new Dictionary<Guid, ArrHealthStatus>());

        _jobs = Substitute.For<IJobManagementService>();
        _jobs.GetAllJobs().ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<JobInfo>>([]));

        _service = new StatsService(Substitute.For<ILogger<StatsService>>(), _context, _health, _jobs, new SqliteDatabaseProvider());
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

    private static JobRun Run(JobType type, JobRunStatus? status, DateTimeOffset startedAt) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Status = status,
        StartedAt = startedAt,
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
    public async Task GetStatsV2Async_AggregatesJobRunsWithinTheTimeframe()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _context.JobRuns.Add(Run(JobType.QueueCleaner, JobRunStatus.Completed, now.AddHours(-2)));
        _context.JobRuns.Add(Run(JobType.QueueCleaner, JobRunStatus.Failed, now.AddHours(-1)));
        _context.JobRuns.Add(Run(JobType.MalwareBlocker, JobRunStatus.Completed, now.AddHours(-3)));
        _context.JobRuns.Add(Run(JobType.QueueCleaner, JobRunStatus.Completed, now.AddHours(-100)));
        await _context.SaveChangesAsync();

        StatsV2Response stats = await _service.GetStatsV2Async(24);

        stats.Jobs.Total.ShouldBe(3);
        stats.Jobs.Completed.ShouldBe(2);
        stats.Jobs.Failed.ShouldBe(1);

        JobTypeV2Stats queueCleaner = stats.Jobs.ByType["QueueCleaner"];
        queueCleaner.Total.ShouldBe(2);
        queueCleaner.Completed.ShouldBe(1);
        queueCleaner.Failed.ShouldBe(1);
        queueCleaner.LastRunAt!.Value.ShouldBe(now.AddHours(-1), TimeSpan.FromSeconds(1));
        queueCleaner.NextRunAt.ShouldBeNull();
    }

    [Fact]
    public async Task GetStatsV2Async_JobsCarryTheNextScheduledRun()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset nextQueueCleanerRun = now.AddMinutes(5);
        DateTimeOffset nextSeekerRun = now.AddMinutes(30);

        _context.JobRuns.Add(Run(JobType.QueueCleaner, JobRunStatus.Completed, now.AddHours(-1)));
        await _context.SaveChangesAsync();

        _jobs.GetAllJobs().ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<JobInfo>>(
        [
            new JobInfo { JobType = nameof(JobType.QueueCleaner), NextRunTime = nextQueueCleanerRun },
            new JobInfo { JobType = nameof(JobType.Seeker), NextRunTime = nextSeekerRun },
        ]));

        StatsV2Response stats = await _service.GetStatsV2Async(24);

        stats.Jobs.ByType["QueueCleaner"].NextRunAt.ShouldBe(nextQueueCleanerRun);

        // A job that never ran in the timeframe is still listed, with its schedule and no runs.
        JobTypeV2Stats seeker = stats.Jobs.ByType["Seeker"];
        seeker.Total.ShouldBe(0);
        seeker.LastRunAt.ShouldBeNull();
        seeker.NextRunAt.ShouldBe(nextSeekerRun);

        stats.Jobs.Total.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatsV2Async_ProjectsCachedHealthSnapshots()
    {
        Guid clientId = Guid.NewGuid();
        Guid instanceId = Guid.NewGuid();
        DateTimeOffset checkedAt = DateTimeOffset.UtcNow.AddMinutes(-2);

        _health.GetAllClientHealth().Returns(new Dictionary<Guid, HealthStatus>
        {
            [clientId] = new()
            {
                ClientId = clientId,
                ClientName = "qbit",
                ClientTypeName = DownloadClientTypeName.qBittorrent,
                IsHealthy = false,
                LastChecked = checkedAt,
                ResponseTime = TimeSpan.FromMilliseconds(250),
                ErrorMessage = "connection refused",
            },
        });
        _health.GetAllArrInstanceHealth().Returns(new Dictionary<Guid, ArrHealthStatus>
        {
            [instanceId] = new()
            {
                InstanceId = instanceId,
                InstanceName = "sonarr",
                InstanceType = InstanceType.Sonarr,
                IsHealthy = true,
                LastChecked = checkedAt,
            },
        });

        StatsV2Response stats = await _service.GetStatsV2Async(24);

        DownloadClientHealthDto client = stats.Health.DownloadClients.ShouldHaveSingleItem();
        client.Id.ShouldBe(clientId);
        client.Name.ShouldBe("qbit");
        client.Type.ShouldBe(nameof(DownloadClientTypeName.qBittorrent));
        client.IsHealthy.ShouldBeFalse();
        client.LastChecked.ShouldBe(checkedAt);
        client.ResponseTimeMs.ShouldBe(250);
        client.ErrorMessage.ShouldBe("connection refused");

        ArrInstanceHealthDto instance = stats.Health.ArrInstances.ShouldHaveSingleItem();
        instance.Id.ShouldBe(instanceId);
        instance.Name.ShouldBe("sonarr");
        instance.Type.ShouldBe(nameof(InstanceType.Sonarr));
        instance.IsHealthy.ShouldBeTrue();
        instance.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task GetTimelineAsync_CountsStrikesOfEveryTypeAndRecoveries()
    {
        _context.Events.Add(Event(EventType.StalledStrike));
        _context.Events.Add(Event(EventType.SlowSpeedStrike));
        _context.Events.Add(Event(EventType.DeadTorrentStrike));
        _context.Events.Add(Event(EventType.StrikeReset));
        _context.Events.Add(Event(EventType.SlowTimeStrike, isDryRun: true));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled));
        await _context.SaveChangesAsync();

        List<TimelineBucketDto> strikes = await _service.GetTimelineAsync("strikesIssued", 24);
        strikes.Sum(b => b.Count).ShouldBe(3);

        List<TimelineBucketDto> strikesWithDryRun = await _service.GetTimelineAsync("strikesIssued", 24, includeDryRun: true);
        strikesWithDryRun.Sum(b => b.Count).ShouldBe(4);

        List<TimelineBucketDto> recovered = await _service.GetTimelineAsync("recovered", 24);
        recovered.Sum(b => b.Count).ShouldBe(1);
    }

    [Fact]
    public async Task GetTimelineAsync_UnknownMetricCountsEveryEventType()
    {
        _context.Events.Add(Event(EventType.StalledStrike));
        _context.Events.Add(Event(EventType.StrikeReset));
        _context.Events.Add(Event(EventType.QueueItemDeleted, deleteReason: DeleteReason.Stalled));
        await _context.SaveChangesAsync();

        List<TimelineBucketDto> events = await _service.GetTimelineAsync("events", 24);
        events.Sum(b => b.Count).ShouldBe(3);

        List<TimelineBucketDto> unknown = await _service.GetTimelineAsync("not-a-metric", 24);
        unknown.Sum(b => b.Count).ShouldBe(3);
    }

    [Fact]
    public async Task GetTimelineAsync_BucketsHourlyForShortTimeframesAndDailyBeyond()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _context.Events.Add(Event(EventType.StrikeReset, timestamp: now));
        _context.Events.Add(Event(EventType.StrikeReset, timestamp: now.AddHours(-2)));
        _context.Events.Add(Event(EventType.StrikeReset, timestamp: now.AddDays(-3)));
        await _context.SaveChangesAsync();

        List<TimelineBucketDto> hourly = await _service.GetTimelineAsync("recovered", 24);
        hourly.Sum(b => b.Count).ShouldBe(2);
        hourly.Count(b => b.Count > 0).ShouldBe(2);

        List<TimelineBucketDto> daily = await _service.GetTimelineAsync("recovered", 168);
        daily.Sum(b => b.Count).ShouldBe(3);
        daily.Count(b => b.Count > 0).ShouldBe(2);
        daily.ShouldAllBe(b => b.Date.TimeOfDay == TimeSpan.Zero);
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
