using Cleanuparr.Api.Controllers;
using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Cleanuparr.Api.Tests.Controllers;

public class EventsControllerTimelineTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly EventsController _controller;

    public EventsControllerTimelineTests()
    {
        _context = SeekerTestDataFactory.CreateEventsContext();
        _controller = new EventsController(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static EventTypeTimelineResponse GetTimeline(ActionResult<EventTypeTimelineResponse> action)
    {
        OkObjectResult ok = action.Result.ShouldBeOfType<OkObjectResult>();
        return ok.Value.ShouldBeOfType<EventTypeTimelineResponse>();
    }

    [Fact]
    public async Task GetTimeline_BucketsEventsByTypeAndDay()
    {
        DateOnly today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        DateTimeOffset sameDay = new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        _context.Events.Add(new AppEvent
        {
            EventType = EventType.FailedImportStrike,
            Message = "active a",
            Severity = EventSeverity.Important,
            Timestamp = sameDay,
        });
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.FailedImportStrike,
            Message = "active b",
            Severity = EventSeverity.Important,
            Timestamp = sameDay.AddHours(-1),
        });
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.StalledStrike,
            Message = "active c",
            Severity = EventSeverity.Important,
            Timestamp = sameDay.AddHours(-2),
        });
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.QueueItemDeleted,
            Message = "older removal",
            Severity = EventSeverity.Important,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-10),
        });
        await _context.SaveChangesAsync();

        EventTypeTimelineResponse timeline = GetTimeline(await _controller.GetTimeline(hours: 24 * 30));

        timeline.Types.ShouldBe(["FailedImportStrike", "StalledStrike", "QueueItemDeleted"]);

        int failedImport = timeline.Buckets.Sum(b => b.Counts.GetValueOrDefault("FailedImportStrike"));
        int stalled = timeline.Buckets.Sum(b => b.Counts.GetValueOrDefault("StalledStrike"));
        int removed = timeline.Buckets.Sum(b => b.Counts.GetValueOrDefault("QueueItemDeleted"));

        failedImport.ShouldBe(2);
        stalled.ShouldBe(1);
        removed.ShouldBe(1);

        EventTypeTimelineBucket todayBucket = timeline.Buckets.Single(b => b.Date == today);
        todayBucket.Counts["FailedImportStrike"].ShouldBe(2);
        todayBucket.Counts["StalledStrike"].ShouldBe(1);
        todayBucket.Counts.ShouldNotContainKey("QueueItemDeleted");
    }

    [Fact]
    public async Task GetTimeline_ExcludesEventsOutsideWindow()
    {
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.QueueItemDeleted,
            Message = "too old",
            Severity = EventSeverity.Important,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-40),
        });
        await _context.SaveChangesAsync();

        EventTypeTimelineResponse timeline = GetTimeline(await _controller.GetTimeline(hours: 24 * 7));

        timeline.Types.ShouldBeEmpty();
        timeline.Buckets.ShouldAllBe(b => b.Counts.Count == 0);
    }
}
