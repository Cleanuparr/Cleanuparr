using System.Text.Json;
using Cleanuparr.Api.Contracts.Responses;
using Cleanuparr.Api.Controllers;
using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Cleanuparr.Persistence.Providers;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Controllers;

/// <summary>
/// Every event endpoint serves the same projected shape.
/// </summary>
public class EventsResponseContractTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly EventsController _controller;
    private readonly Guid _trackingId = Guid.NewGuid();

    public EventsResponseContractTests()
    {
        _context = ConfigControllerTestDataFactory.CreateEventsContext();
        _controller = new EventsController(_context, new SqliteDatabaseProvider());
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<AppEvent> SeedEventAsync()
    {
        AppEvent appEvent = new()
        {
            EventType = EventType.StalledStrike,
            Message = "a stalled strike",
            Severity = EventSeverity.Important,
            Timestamp = DateTimeOffset.UtcNow,
            TrackingId = _trackingId,
            ItemTitle = "Some.Item",
            ItemHash = "abc123",
            StrikeCount = 1,
        };

        _context.Events.Add(appEvent);
        await _context.SaveChangesAsync();
        return appEvent;
    }

    [Fact]
    public async Task GetEvents_ReturnsTheDocumentedPaginationWrapperKeys()
    {
        await SeedEventAsync();

        ActionResult<PaginatedResult<EventListItem>> result = await _controller.GetEvents();

        ResponseContract.Keys(result.Result!).ShouldBe(
        [
            "items",
            "page",
            "pageSize",
            "totalCount",
            "totalPages",
        ]);
    }

    [Fact]
    public async Task GetEvents_ReturnsTheDocumentedListItemKeys()
    {
        await SeedEventAsync();

        ActionResult<PaginatedResult<EventListItem>> result = await _controller.GetEvents();

        FirstItemKeys(result.Result!).ShouldBe(ListItemKeys);
    }

    [Fact]
    public async Task GetEvent_ReturnsTheDocumentedListItemKeys()
    {
        AppEvent seeded = await SeedEventAsync();

        ActionResult<EventListItem> result = await _controller.GetEvent(seeded.Id);

        ResponseContract.Keys(result.Result!).ShouldBe(ListItemKeys);
    }

    [Fact]
    public async Task GetEvent_ReturnsNotFound_ForAnUnknownId()
    {
        ActionResult<EventListItem> result = await _controller.GetEvent(Guid.NewGuid());

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetEventsByTracking_ReturnsTheDocumentedListItemKeys()
    {
        await SeedEventAsync();

        ActionResult<List<EventListItem>> result = await _controller.GetEventsByTracking(_trackingId);

        ResponseContract.FirstItemKeys(result.Result!).ShouldBe(ListItemKeys);
    }

    [Fact]
    public async Task SingleEventShape_MatchesTheListShape()
    {
        AppEvent seeded = await SeedEventAsync();

        ActionResult<EventListItem> single = await _controller.GetEvent(seeded.Id);
        ActionResult<PaginatedResult<EventListItem>> list = await _controller.GetEvents();

        ResponseContract.Keys(single.Result!).ShouldBe(FirstItemKeys(list.Result!));
    }

    [Fact]
    public void ListItemShape_DropsTheNotificationOnlyKeys()
    {
        ListItemKeys.ShouldNotContain("downloadClientName");
        ListItemKeys.ShouldNotContain("downloadClientType");
        ListItemKeys.ShouldNotContain("instanceType");
        ListItemKeys.ShouldNotContain("instanceUrl");
    }

    /// <summary>
    /// What the list endpoint projects into.
    /// </summary>
    private static readonly string[] ListItemKeys =
    [
        "arrInstanceId",
        "cleanReason",
        "cleanedCategory",
        "completedAt",
        "cycleId",
        "deleteReason",
        "downloadClientId",
        "eventType",
        "failedImportReasons",
        "grabbedItems",
        "id",
        "isCategoryTag",
        "isDryRun",
        "itemHash",
        "itemTitle",
        "jobRunId",
        "message",
        "newCategory",
        "oldCategory",
        "removeFromClient",
        "searchReason",
        "searchStatus",
        "searchType",
        "seedRatio",
        "seedingTimeHours",
        "severity",
        "strikeCount",
        "strikeId",
        "timestamp",
        "trackingId",
    ];

    private static IReadOnlyList<string> FirstItemKeys(IActionResult result)
    {
        JsonElement items = ResponseContract.Body(result).GetProperty("items");
        return ResponseContract.Keys(items.EnumerateArray().First());
    }
}
