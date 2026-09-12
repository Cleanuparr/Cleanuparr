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
/// The single-event endpoints serve a wider shape than the list projection.
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
    public async Task GetEvent_ReturnsTheRawEntityKeys_NotTheListItemKeys()
    {
        AppEvent seeded = await SeedEventAsync();

        ActionResult<AppEvent> result = await _controller.GetEvent(seeded.Id);

        ResponseContract.Keys(result.Result!).ShouldBe(RawEntityKeys);
    }

    [Fact]
    public async Task GetEventsByTracking_ReturnsTheRawEntityKeys_NotTheListItemKeys()
    {
        await SeedEventAsync();

        ActionResult<List<AppEvent>> result = await _controller.GetEventsByTracking(_trackingId);

        ResponseContract.FirstItemKeys(result.Result!).ShouldBe(RawEntityKeys);
    }

    [Fact]
    public async Task SingleEventShape_AddsTheNotificationOnlyKeysThatTheListShapeOmits()
    {
        RawEntityKeys.Except(ListItemKeys).ShouldBe(
        [
            "downloadClientName",
            "downloadClientType",
            "instanceType",
            "instanceUrl",
        ]);
        ListItemKeys.Except(RawEntityKeys).ShouldBeEmpty();
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

    /// <summary>
    /// The four extra keys are [NotMapped] notification fields, null over the wire.
    /// </summary>
    private static readonly string[] RawEntityKeys =
    [
        "arrInstanceId",
        "cleanReason",
        "cleanedCategory",
        "completedAt",
        "cycleId",
        "deleteReason",
        "downloadClientId",
        "downloadClientName",
        "downloadClientType",
        "eventType",
        "failedImportReasons",
        "grabbedItems",
        "id",
        "instanceType",
        "instanceUrl",
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
