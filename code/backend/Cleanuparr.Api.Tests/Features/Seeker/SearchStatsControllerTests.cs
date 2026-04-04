using System.Text.Json;
using Cleanuparr.Api.Features.Seeker.Controllers;
using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Events;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Seeker;

public class SearchStatsControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly EventsContext _eventsContext;
    private readonly SearchStatsController _controller;

    public SearchStatsControllerTests()
    {
        _dataContext = SeekerTestDataFactory.CreateDataContext();
        _eventsContext = SeekerTestDataFactory.CreateEventsContext();
        _controller = new SearchStatsController(_dataContext, _eventsContext);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        _eventsContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private static JsonElement GetResponseBody(IActionResult result)
    {
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        return JsonDocument.Parse(json).RootElement;
    }

    #region GetEvents with SearchEventData

    [Fact]
    public async Task GetEvents_WithNoSearchEventData_ReturnsUnknownDefaults()
    {
        AddSearchEvent();

        var result = await _controller.GetEvents();
        var body = GetResponseBody(result);

        var item = body.GetProperty("Items")[0];
        item.GetProperty("ItemTitle").GetString().ShouldBe("Unknown");
    }

    [Fact]
    public async Task GetEvents_WithSearchEventData_ReturnsAllFields()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);

        AddSearchEvent(
            arrInstanceId: radarr.Id,
            itemTitle: "Movie A",
            searchType: SeekerSearchType.Proactive,
            searchReason: SeekerSearchReason.Missing,
            grabbedItems: ["Movie A (2024)"]);

        var result = await _controller.GetEvents();
        var body = GetResponseBody(result);

        var item = body.GetProperty("Items")[0];
        item.GetProperty("ArrInstanceId").GetString().ShouldBe(radarr.Id.ToString());
        item.GetProperty("InstanceType").GetString().ShouldBe(nameof(InstanceType.Radarr));
        item.GetProperty("ItemTitle").GetString().ShouldBe("Movie A");
        item.GetProperty("SearchType").GetString().ShouldBe(nameof(SeekerSearchType.Proactive));
        item.GetProperty("SearchReason").GetString().ShouldBe(nameof(SeekerSearchReason.Missing));
        item.GetProperty("GrabbedItems")[0].GetString().ShouldBe("Movie A (2024)");
    }

    [Fact]
    public async Task GetEvents_WithReplacementSearchType_ParsesCorrectEnum()
    {
        AddSearchEvent(
            itemTitle: "Series A",
            searchType: SeekerSearchType.Replacement,
            searchReason: SeekerSearchReason.Replacement);

        var result = await _controller.GetEvents();
        var body = GetResponseBody(result);

        var item = body.GetProperty("Items")[0];
        item.GetProperty("SearchType").GetString().ShouldBe(nameof(SeekerSearchType.Replacement));
        item.GetProperty("SearchReason").GetString().ShouldBe(nameof(SeekerSearchReason.Replacement));
    }

    #endregion

    #region GetEvents Filtering

    [Fact]
    public async Task GetEvents_WithInstanceIdFilter_FiltersByArrInstanceId()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        var sonarr = SeekerTestDataFactory.AddSonarrInstance(_dataContext);

        AddSearchEvent(arrInstanceId: radarr.Id, itemTitle: "Radarr Movie");
        AddSearchEvent(arrInstanceId: sonarr.Id, itemTitle: "Sonarr Series");

        var result = await _controller.GetEvents(instanceId: radarr.Id);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
        body.GetProperty("Items")[0].GetProperty("ArrInstanceId").GetString().ShouldBe(radarr.Id.ToString());
    }

    [Fact]
    public async Task GetEvents_WithCycleIdFilter_ReturnsOnlyMatchingCycle()
    {
        var cycleA = Guid.NewGuid();
        var cycleB = Guid.NewGuid();

        AddSearchEvent(cycleId: cycleA, itemTitle: "Cycle A Movie");
        AddSearchEvent(cycleId: cycleB, itemTitle: "Cycle B Movie");

        var result = await _controller.GetEvents(cycleId: cycleA);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
        body.GetProperty("Items")[0].GetProperty("ItemTitle").GetString().ShouldBe("Cycle A Movie");
    }

    [Fact]
    public async Task GetEvents_WithSearchFilter_FiltersOnItemTitle()
    {
        AddSearchEvent(itemTitle: "The Matrix");
        AddSearchEvent(itemTitle: "Breaking Bad");

        var result = await _controller.GetEvents(search: "matrix");
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task GetEvents_WithPagination_ReturnsCorrectPageAndCount()
    {
        for (int i = 0; i < 5; i++)
        {
            AddSearchEvent(itemTitle: $"Event {i}");
        }

        var result = await _controller.GetEvents(page: 2, pageSize: 2);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(5);
        body.GetProperty("TotalPages").GetInt32().ShouldBe(3);
        body.GetProperty("Page").GetInt32().ShouldBe(2);
        body.GetProperty("Items").GetArrayLength().ShouldBe(2);
    }

    #endregion

    #region Helpers

    private void AddSearchEvent(
        string? itemTitle = null,
        SeekerSearchType searchType = SeekerSearchType.Proactive,
        SeekerSearchReason searchReason = SeekerSearchReason.Missing,
        List<string>? grabbedItems = null,
        Guid? arrInstanceId = null,
        Guid? cycleId = null,
        SearchCommandStatus? searchStatus = null)
    {
        var appEvent = new AppEvent
        {
            EventType = EventType.SearchTriggered,
            Message = "Search triggered",
            Severity = EventSeverity.Information,
            ArrInstanceId = arrInstanceId,
            CycleId = cycleId,
            SearchStatus = searchStatus,
            Timestamp = DateTime.UtcNow
        };

        _eventsContext.Events.Add(appEvent);
        _eventsContext.SaveChanges();

        if (itemTitle is not null)
        {
            _eventsContext.SearchEventData.Add(new SearchEventData
            {
                AppEventId = appEvent.Id,
                ItemTitle = itemTitle,
                SearchType = searchType,
                SearchReason = searchReason,
                GrabbedItems = grabbedItems ?? [],
            });
            _eventsContext.SaveChanges();
        }
    }

    #endregion
}
