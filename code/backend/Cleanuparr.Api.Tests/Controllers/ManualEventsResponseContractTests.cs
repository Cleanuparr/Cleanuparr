using System.Text.Json;
using Cleanuparr.Api.Contracts.Responses;
using Cleanuparr.Api.Controllers;
using Cleanuparr.Api.Features.Events.Contracts.Responses;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Controllers;

/// <summary>
/// Pins the manual event response shape.
/// </summary>
public class ManualEventsResponseContractTests : IDisposable
{
    private readonly EventsContext _context;
    private readonly ManualEventsController _controller;

    public ManualEventsResponseContractTests()
    {
        _context = ConfigControllerTestDataFactory.CreateEventsContext();
        _controller = new ManualEventsController(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<ManualEvent> SeedEventAsync()
    {
        ManualEvent manualEvent = new()
        {
            Type = ManualEventType.RecurringDownload,
            Message = "a recurring download",
            Severity = EventSeverity.Warning,
            Timestamp = DateTimeOffset.UtcNow,
            ItemTitle = "Some.Item",
            ItemHash = "abc123",
            StrikeCount = 1,
        };

        _context.ManualEvents.Add(manualEvent);
        await _context.SaveChangesAsync();
        return manualEvent;
    }

    [Fact]
    public async Task GetManualEvents_ReturnsTheDocumentedPaginationWrapperKeys()
    {
        await SeedEventAsync();

        ActionResult<PaginatedResult<ManualEventResponse>> result = await _controller.GetManualEvents();

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
    public async Task GetManualEvents_ReturnsTheDocumentedItemKeys()
    {
        await SeedEventAsync();

        ActionResult<PaginatedResult<ManualEventResponse>> result = await _controller.GetManualEvents();

        FirstItemKeys(result.Result!).ShouldBe(ResponseKeys);
    }

    [Fact]
    public async Task GetManualEvent_ReturnsTheSameKeysAsTheListItems()
    {
        ManualEvent seeded = await SeedEventAsync();

        ActionResult<ManualEventResponse> result = await _controller.GetManualEvent(seeded.Id);

        ResponseContract.Keys(result.Result!).ShouldBe(ResponseKeys);
    }

    [Fact]
    public async Task GetManualEvent_ReturnsNotFound_ForAnUnknownId()
    {
        ActionResult<ManualEventResponse> result = await _controller.GetManualEvent(Guid.NewGuid());

        result.Result.ShouldBeOfType<NotFoundResult>();
    }

    /// <summary>
    /// Every column of the entity, all of them persisted.
    /// </summary>
    private static readonly string[] ResponseKeys =
    [
        "downloadClientName",
        "downloadClientType",
        "id",
        "instanceType",
        "instanceUrl",
        "isDryRun",
        "isResolved",
        "itemHash",
        "itemTitle",
        "jobRunId",
        "message",
        "resolvedAt",
        "severity",
        "strikeCount",
        "timestamp",
        "type",
    ];

    private static IReadOnlyList<string> FirstItemKeys(IActionResult result)
    {
        JsonElement items = ResponseContract.Body(result).GetProperty("items");
        return ResponseContract.Keys(items.EnumerateArray().First());
    }
}
