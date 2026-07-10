using Cleanuparr.Api.Contracts.Responses;
using Cleanuparr.Api.Controllers;
using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Providers;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Cleanuparr.Api.Tests.Controllers;

/// <summary>
/// Verifies the events list endpoint's ordering, filtering, search, and primitive-collection round-tripping.
/// Runs against real SQLite so the projection is actually translated.
/// </summary>
public class EventsControllerMergeTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly EventsController _controller;

    public EventsControllerMergeTests()
    {
        _context = SeekerTestDataFactory.CreateEventsContext();
        _controller = new EventsController(_context, new SqliteDatabaseProvider());
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedAsync()
    {
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.FailedImportStrike,
            Message = "active",
            Severity = EventSeverity.Important,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-1),
            ItemTitle = "Active Item",
            FailedImportReasons = ["reason one", "reason two"],
            GrabbedItems = ["grab one"],
        });
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.QueueItemDeleted,
            Message = "archived",
            Severity = EventSeverity.Important,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-100),
            ItemTitle = "Archived Item",
        });
        await _context.SaveChangesAsync();
    }

    private static PaginatedResult<EventListItem> GetPage(ActionResult<PaginatedResult<EventListItem>> action)
    {
        OkObjectResult ok = action.Result.ShouldBeOfType<OkObjectResult>();
        return ok.Value.ShouldBeOfType<PaginatedResult<EventListItem>>();
    }

    [Fact]
    public async Task GetEvents_OrdersNewestFirst_AndRoundTripsCollections()
    {
        await SeedAsync();

        PaginatedResult<EventListItem> page = GetPage(await _controller.GetEvents());

        page.TotalCount.ShouldBe(2);
        page.Items.Count.ShouldBe(2);
        page.Items[0].Message.ShouldBe("active");   // newer
        page.Items[1].Message.ShouldBe("archived");

        // The primitive-collection columns must survive the Concat projection.
        page.Items[0].FailedImportReasons.ShouldBe(["reason one", "reason two"]);
        page.Items[0].GrabbedItems.ShouldBe(["grab one"]);
    }

    [Fact]
    public async Task GetEvents_EventTypeFilter_Applies()
    {
        await SeedAsync();

        PaginatedResult<EventListItem> page = GetPage(await _controller.GetEvents(eventType: nameof(EventType.QueueItemDeleted)));

        page.TotalCount.ShouldBe(1);
        page.Items[0].Message.ShouldBe("archived");
    }

    [Fact]
    public async Task GetEvents_SearchFilter_MatchesArchivedItemTitle()
    {
        await SeedAsync();

        PaginatedResult<EventListItem> page = GetPage(await _controller.GetEvents(search: "Archived"));

        page.TotalCount.ShouldBe(1);
        page.Items[0].Message.ShouldBe("archived");
    }
}
