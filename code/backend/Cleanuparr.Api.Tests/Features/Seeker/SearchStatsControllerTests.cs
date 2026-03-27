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

    #region ParseEventData (tested via GetEvents)

    [Fact]
    public async Task GetEvents_WithNullEventData_ReturnsUnknownDefaults()
    {
        AddSearchEvent(data: null);

        var result = await _controller.GetEvents();
        var body = GetResponseBody(result);

        var item = body.GetProperty("Items")[0];
        item.GetProperty("InstanceName").GetString().ShouldBe("Unknown");
        item.GetProperty("ItemCount").GetInt32().ShouldBe(0);
        item.GetProperty("Items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetEvents_WithValidFullJson_ParsesAllFields()
    {
        var data = JsonSerializer.Serialize(new
        {
            InstanceName = "My Radarr",
            ItemCount = 3,
            Items = new[] { "Movie A", "Movie B", "Movie C" },
            SearchType = "Proactive",
            GrabbedItems = new[] { new { Title = "Movie A", Quality = "Bluray-1080p" } }
        });
        AddSearchEvent(data: data);

        var result = await _controller.GetEvents();
        var body = GetResponseBody(result);

        var item = body.GetProperty("Items")[0];
        item.GetProperty("InstanceName").GetString().ShouldBe("My Radarr");
        item.GetProperty("ItemCount").GetInt32().ShouldBe(3);
        item.GetProperty("Items").GetArrayLength().ShouldBe(3);
        item.GetProperty("Items")[0].GetString().ShouldBe("Movie A");
        item.GetProperty("SearchType").GetString().ShouldBe(nameof(SeekerSearchType.Proactive));
    }

    [Fact]
    public async Task GetEvents_WithPartialJson_ReturnsDefaultsForMissingFields()
    {
        // Only InstanceName is present, other fields missing
        var data = JsonSerializer.Serialize(new { InstanceName = "Partial Instance" });
        AddSearchEvent(data: data);

        var result = await _controller.GetEvents();
        var body = GetResponseBody(result);

        var item = body.GetProperty("Items")[0];
        item.GetProperty("InstanceName").GetString().ShouldBe("Partial Instance");
        item.GetProperty("ItemCount").GetInt32().ShouldBe(0);
        item.GetProperty("Items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetEvents_WithMalformedJson_ReturnsUnknownDefaults()
    {
        AddSearchEvent(data: "not valid json {{{");

        var result = await _controller.GetEvents();
        var body = GetResponseBody(result);

        var item = body.GetProperty("Items")[0];
        item.GetProperty("InstanceName").GetString().ShouldBe("Unknown");
        item.GetProperty("ItemCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetEvents_WithSearchTypeReplacement_ParsesCorrectEnum()
    {
        var data = JsonSerializer.Serialize(new
        {
            InstanceName = "Sonarr",
            SearchType = "Replacement"
        });
        AddSearchEvent(data: data);

        var result = await _controller.GetEvents();
        var body = GetResponseBody(result);

        var item = body.GetProperty("Items")[0];
        item.GetProperty("SearchType").GetString().ShouldBe(nameof(SeekerSearchType.Replacement));
    }

    #endregion

    #region GetEvents Filtering

    [Fact]
    public async Task GetEvents_WithInstanceIdFilter_FiltersViaInstanceUrl()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        var sonarr = SeekerTestDataFactory.AddSonarrInstance(_dataContext);

        // Event matching radarr's URL
        AddSearchEvent(instanceUrl: radarr.Url.ToString(), instanceType: InstanceType.Radarr,
            data: JsonSerializer.Serialize(new { InstanceName = "Radarr Event" }));
        // Event matching sonarr's URL
        AddSearchEvent(instanceUrl: sonarr.Url.ToString(), instanceType: InstanceType.Sonarr,
            data: JsonSerializer.Serialize(new { InstanceName = "Sonarr Event" }));

        var result = await _controller.GetEvents(instanceId: radarr.Id);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
        body.GetProperty("Items")[0].GetProperty("InstanceName").GetString().ShouldBe("Radarr Event");
    }

    [Fact]
    public async Task GetEvents_WithCycleIdFilter_ReturnsOnlyMatchingCycle()
    {
        var cycleA = Guid.NewGuid();
        var cycleB = Guid.NewGuid();

        AddSearchEvent(cycleId: cycleA, data: JsonSerializer.Serialize(new { InstanceName = "Cycle A" }));
        AddSearchEvent(cycleId: cycleB, data: JsonSerializer.Serialize(new { InstanceName = "Cycle B" }));

        var result = await _controller.GetEvents(cycleId: cycleA);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
        body.GetProperty("Items")[0].GetProperty("InstanceName").GetString().ShouldBe("Cycle A");
    }

    [Fact]
    public async Task GetEvents_WithSearchFilter_FiltersOnDataField()
    {
        AddSearchEvent(data: JsonSerializer.Serialize(new { InstanceName = "Radarr", Items = new[] { "The Matrix" } }));
        AddSearchEvent(data: JsonSerializer.Serialize(new { InstanceName = "Sonarr", Items = new[] { "Breaking Bad" } }));

        var result = await _controller.GetEvents(search: "matrix");
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task GetEvents_WithPagination_ReturnsCorrectPageAndCount()
    {
        for (int i = 0; i < 5; i++)
        {
            AddSearchEvent(data: JsonSerializer.Serialize(new { InstanceName = $"Event {i}" }));
        }

        var result = await _controller.GetEvents(page: 2, pageSize: 2);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(5);
        body.GetProperty("TotalPages").GetInt32().ShouldBe(3); // ceil(5/2) = 3
        body.GetProperty("Page").GetInt32().ShouldBe(2);
        body.GetProperty("Items").GetArrayLength().ShouldBe(2);
    }

    #endregion

    #region Helpers

    private void AddSearchEvent(
        string? data = null,
        string? instanceUrl = null,
        InstanceType? instanceType = null,
        Guid? cycleId = null,
        SearchCommandStatus? searchStatus = null)
    {
        _eventsContext.Events.Add(new AppEvent
        {
            EventType = EventType.SearchTriggered,
            Message = "Search triggered",
            Severity = EventSeverity.Information,
            Data = data,
            InstanceUrl = instanceUrl,
            InstanceType = instanceType,
            CycleId = cycleId,
            SearchStatus = searchStatus,
            Timestamp = DateTime.UtcNow
        });
        _eventsContext.SaveChanges();
    }

    #endregion
}
