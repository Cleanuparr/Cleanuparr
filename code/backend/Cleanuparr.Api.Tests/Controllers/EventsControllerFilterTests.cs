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
/// The sentinel is not a database value.
/// A filter naming it has to be dropped before the query.
/// </summary>
public class EventsControllerFilterTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly EventsController _controller;

    public EventsControllerFilterTests()
    {
        _context = SeekerTestDataFactory.CreateEventsContext();
        _controller = new EventsController(_context, new SqliteDatabaseProvider());
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedOneEventAsync()
    {
        _context.Events.Add(new AppEvent
        {
            EventType = EventType.StalledStrike,
            Message = "an event",
            Severity = EventSeverity.Important,
            Timestamp = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();
    }

    private static PaginatedResult<EventListItem> GetEvents(ActionResult<PaginatedResult<EventListItem>> action)
    {
        OkObjectResult ok = action.Result.ShouldBeOfType<OkObjectResult>();
        return ok.Value.ShouldBeOfType<PaginatedResult<EventListItem>>();
    }

    [Theory]
    [InlineData(EnumSentinel.Unknown)]
    [InlineData("999")]
    public async Task GetEvents_WithAnUnusableEventTypeFilter_IgnoresIt(string eventType)
    {
        await SeedOneEventAsync();

        PaginatedResult<EventListItem> result = GetEvents(await _controller.GetEvents(eventType: eventType));

        result.TotalCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(EnumSentinel.Unknown)]
    [InlineData("999")]
    public async Task GetEvents_WithAnUnusableSeverityFilter_IgnoresIt(string severity)
    {
        await SeedOneEventAsync();

        PaginatedResult<EventListItem> result = GetEvents(await _controller.GetEvents(severity: severity));

        result.TotalCount.ShouldBe(1);
    }
}
