using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.Sonarr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class SportarrClientTests
{
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly SportarrClient _client;
    private readonly ArrInstance _arrInstance;

    public SportarrClientTests()
    {
        var logger = Substitute.For<ILogger<SportarrClient>>();
        var striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        var httpClient = new HttpClient(_httpMessageHandler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new SportarrClient(logger, httpClientFactory, striker, _dryRunInterceptor);
        _arrInstance = new ArrInstance
        {
            Name = "sportarr",
            Url = new Uri("http://localhost:1867/"),
            ApiKey = "api-key",
        };

        _dryRunInterceptor.IsDryRunEnabled().Returns(false);
        _dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Func<Task<HttpResponseMessage>>>(), Arg.Any<string?>())
            .Returns(async ci =>
            {
                Func<Task<HttpResponseMessage>> action = ci.Arg<Func<Task<HttpResponseMessage>>>();
                return await action();
            });
    }

    [Fact]
    public async Task HealthCheckAsync_UsesInheritedV3SystemStatus()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.HealthCheckAsync(_arrInstance);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/system/status");
    }

    [Fact]
    public void SupportsForceImport_IsFalse()
    {
        _client.SupportsForceImport.ShouldBeFalse();
    }

    #region SearchItemsAsync

    [Fact]
    public async Task SearchItemsAsync_Episode_PostsEventIdToNativeSearchQueue()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new { id = "guid", taskId = 77 })));
        HashSet<SearchItem> items = [new SeriesSearchItem { Id = 5, SeriesId = 10, SearchType = SeriesSearchType.Episode }];

        // Act
        List<long> ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBe(new long[] { 77 });
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.ShouldBe("/api/search/queue");
        _httpMessageHandler.CapturedRequestBodies[0].ShouldBe("{\"eventId\":5}");
    }

    [Fact]
    public async Task SearchItemsAsync_MultipleEpisodes_QueuesOneSearchPerEvent()
    {
        // Arrange
        int next = 100;
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new { taskId = next++ })));
        HashSet<SearchItem> items =
        [
            new SeriesSearchItem { Id = 1, SeriesId = 10, SearchType = SeriesSearchType.Episode },
            new SeriesSearchItem { Id = 2, SeriesId = 10, SearchType = SeriesSearchType.Episode },
        ];

        // Act
        List<long> ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBe(new long[] { 100, 101 });
        _httpMessageHandler.CapturedRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SearchItemsAsync_ResponseWithoutTaskId_Throws()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new { id = "guid" })));
        HashSet<SearchItem> items = [new SeriesSearchItem { Id = 5, SeriesId = 10, SearchType = SeriesSearchType.Episode }];

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _client.SearchItemsAsync(_arrInstance, items));
    }

    [Fact]
    public async Task SearchItemsAsync_SeasonItem_Throws()
    {
        // Arrange
        HashSet<SearchItem> items = [new SeriesSearchItem { Id = 3, SeriesId = 10, SearchType = SeriesSearchType.Season }];

        // Act & Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _client.SearchItemsAsync(_arrInstance, items));
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    #endregion

    #region GetCommandStatusAsync

    [Theory]
    [InlineData("Queued", null, ArrCommandState.Queued)]
    [InlineData("Running", null, ArrCommandState.Started)]
    [InlineData("Completed", null, ArrCommandState.Completed)]
    [InlineData("Failed", null, ArrCommandState.Completed)]
    [InlineData("Failed", "indexer down", ArrCommandState.Failed)]
    [InlineData("Cancelled", null, ArrCommandState.Failed)]
    [InlineData("Aborting", null, ArrCommandState.Failed)]
    [InlineData("Surprise", null, ArrCommandState.Unknown)]
    public async Task GetCommandStatusAsync_MapsTaskStatus(string status, string? exception, ArrCommandState expected)
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new { id = 77, status, exception })));

        // Act
        ArrCommandStatus result = await _client.GetCommandStatusAsync(_arrInstance, 77);

        // Assert
        result.Id.ShouldBe(77);
        result.Status.ShouldBe(expected);
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/task/77");
    }

    [Fact]
    public async Task GetCommandStatusAsync_TaskNotFound_ThrowsNotFound()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.NotFound);

        // Act
        HttpRequestException exception = await Should.ThrowAsync<HttpRequestException>(
            () => _client.GetCommandStatusAsync(_arrInstance, 77));

        // Assert
        exception.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region GetCommandsAsync

    [Fact]
    public async Task GetCommandsAsync_ReadsTaskListAndMapsStatuses()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new object[]
        {
            new { id = 3, status = "Running" },
            new { id = 2, status = "Failed", exception = (string?)null },
            new { id = 1, status = "Failed", exception = "boom" },
        })));

        // Act
        List<ArrCommandStatus> result = await _client.GetCommandsAsync(_arrInstance);

        // Assert
        result.Select(x => (x.Id, x.Status)).ShouldBe(new[]
        {
            (3L, ArrCommandState.Started),
            (2L, ArrCommandState.Completed),
            (1L, ArrCommandState.Failed),
        });
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/task");
        request.RequestUri.Query.ShouldBe("?pageSize=500");
    }

    #endregion

    #region GetMissingEpisodesAsync

    [Fact]
    public async Task GetMissingEpisodesAsync_MapsNativeEventsToEpisodes()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new
        {
            events = new object[]
            {
                new { id = 7, title = "UFC 300", leagueId = 2, seasonNumber = 2024, episodeNumber = 4, eventDate = "2024-04-14T02:00:00", monitored = true, hasFile = false, status = "Finished" },
                new { id = 8, title = "UFC 301", leagueId = 2, eventDate = "2025-05-04T02:00:00", monitored = true, hasFile = false, status = "Postponed" },
                new { id = 9, title = "UFC 302", leagueId = 2, eventDate = "2025-06-01T02:00:00", monitored = true, hasFile = false, status = "cancelled" },
            },
            page = 1,
            pageSize = 1000,
            totalRecords = 3,
        })));

        // Act
        List<SearchableEpisode> result = await _client.GetMissingEpisodesAsync(_arrInstance, monitored: true);

        // Assert
        result.Count.ShouldBe(3);
        SearchableEpisode first = result[0];
        first.Id.ShouldBe(7);
        first.SeriesId.ShouldBe(2);
        first.SeasonNumber.ShouldBe(2024);
        first.EpisodeNumber.ShouldBe(4);
        first.Title.ShouldBe("UFC 300");
        first.AirDateUtc.ShouldBe(new DateTimeOffset(2024, 4, 14, 2, 0, 0, TimeSpan.Zero));
        first.Monitored.ShouldBeTrue();
        first.HasFile.ShouldBeFalse();
        first.Unsearchable.ShouldBeFalse();
        result[1].SeasonNumber.ShouldBe(2025);
        result[1].EpisodeNumber.ShouldBe(0);
        result[1].Unsearchable.ShouldBeTrue();
        result[2].Unsearchable.ShouldBeTrue();
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/wanted/missing");
        request.RequestUri.Query.ShouldBe("?page=1&pageSize=1000");
    }

    [Fact]
    public async Task GetMissingEpisodesAsync_EventDateWithOffset_KeepsSameInstant()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new
        {
            events = new object[]
            {
                new { id = 7, leagueId = 2, eventDate = "2024-04-14T04:00:00+02:00", monitored = true, hasFile = false },
            },
            page = 1,
            pageSize = 1000,
            totalRecords = 1,
        })));

        // Act
        List<SearchableEpisode> result = await _client.GetMissingEpisodesAsync(_arrInstance, monitored: true);

        // Assert
        result[0].AirDateUtc.ShouldBe(new DateTimeOffset(2024, 4, 14, 2, 0, 0, TimeSpan.Zero));
        result[0].SeasonNumber.ShouldBe(2024);
    }

    [Fact]
    public async Task GetMissingEpisodesAsync_EventDateWithoutOffset_IsReadAsUtc()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new
        {
            events = new object[]
            {
                new { id = 7, leagueId = 2, eventDate = "2024-04-14T02:00:00", monitored = true, hasFile = false },
            },
            page = 1,
            pageSize = 1000,
            totalRecords = 1,
        })));

        // Act
        List<SearchableEpisode> result = await _client.GetMissingEpisodesAsync(_arrInstance, monitored: true);

        // Assert
        result[0].AirDateUtc.ShouldBe(new DateTimeOffset(2024, 4, 14, 2, 0, 0, TimeSpan.Zero));
        result[0].AirDateUtc!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetMissingEpisodesAsync_NullEventDate_LeavesAirDateNull()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new
        {
            events = new object[]
            {
                new { id = 7, leagueId = 2, eventDate = (string?)null, monitored = true, hasFile = false },
            },
            page = 1,
            pageSize = 1000,
            totalRecords = 1,
        })));

        // Act
        List<SearchableEpisode> result = await _client.GetMissingEpisodesAsync(_arrInstance, monitored: true);

        // Assert
        result[0].AirDateUtc.ShouldBeNull();
        result[0].SeasonNumber.ShouldBe(0);
    }

    [Fact]
    public async Task GetMissingEpisodesAsync_PagesUntilTotalReached()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            bool firstPage = req.RequestUri!.Query.StartsWith("?page=1");
            object[] events = firstPage
                ? [new { id = 1, leagueId = 2, eventDate = "2024-01-01T00:00:00", monitored = true }]
                : [new { id = 2, leagueId = 2, eventDate = "2024-01-02T00:00:00", monitored = true }];
            return Task.FromResult(JsonResponse(new { events, totalRecords = 2 }));
        });

        // Act
        List<SearchableEpisode> result = await _client.GetMissingEpisodesAsync(_arrInstance, monitored: true);

        // Assert
        result.Select(x => x.Id).ShouldBe(new long[] { 1, 2 });
        _httpMessageHandler.CapturedRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetMissingEpisodesAsync_TotalRecordsGrowsEveryPage_StopsAtFirstPageTotal()
    {
        // Arrange
        int calls = 0;
        _httpMessageHandler.SetupResponse((_, _) =>
        {
            calls++;
            object[] events = [new { id = calls, leagueId = 2, eventDate = "2024-01-01T00:00:00", monitored = true }];
            return Task.FromResult(JsonResponse(new { events, totalRecords = calls + 1 }));
        });

        // Act
        List<SearchableEpisode> result = await _client.GetMissingEpisodesAsync(_arrInstance, monitored: true);

        // Assert
        result.Count.ShouldBe(2);
        _httpMessageHandler.CapturedRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetMissingEpisodesAsync_Unmonitored_ReturnsEmptyWithoutRequest()
    {
        // Act
        List<SearchableEpisode> result = await _client.GetMissingEpisodesAsync(_arrInstance, monitored: false);

        // Assert
        result.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    #endregion

    #region GetCutoffUnmetEpisodesAsync

    [Fact]
    public async Task GetCutoffUnmetEpisodesAsync_Unmonitored_ReturnsEmptyWithoutRequest()
    {
        // Act
        List<SearchableEpisode> result = await _client.GetCutoffUnmetEpisodesAsync(_arrInstance, monitored: false);

        // Assert
        result.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCutoffUnmetEpisodesAsync_Monitored_ReadsV3CutoffList()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new
        {
            totalRecords = 1,
            records = new object[] { new { id = 4, seriesId = 2, seasonNumber = 2024, episodeNumber = 1, monitored = true, hasFile = true } },
        })));

        // Act
        List<SearchableEpisode> result = await _client.GetCutoffUnmetEpisodesAsync(_arrInstance, monitored: true);

        // Assert
        result.Select(x => x.Id).ShouldBe(new long[] { 4 });
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/wanted/cutoff");
    }

    #endregion

    #region GetRecentlyGrabbedEpisodesAsync

    [Fact]
    public async Task GetRecentlyGrabbedEpisodesAsync_KeepsGrabsSinceCutoffAndStopsAtFirstOlderRow()
    {
        // Arrange
        DateTimeOffset since = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new
        {
            records = new object[]
            {
                new { episodeId = 1, eventType = "grabbed", date = "2026-10-31T10:00:00" },
                new { episodeId = 2, eventType = "downloadFolderImported", date = "2026-10-31T09:00:00" },
                new { episodeId = 3, eventType = "grabbed", date = "2026-10-30T13:00:00.0000000Z" },
                new { episodeId = 4, eventType = "grabbed", date = "2026-10-30T11:00:00" },
                new { episodeId = 5, eventType = "grabbed", date = "2026-10-30T10:00:00" },
            },
        })));

        // Act
        HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)> result =
            await _client.GetRecentlyGrabbedEpisodesAsync(_arrInstance, since);

        // Assert
        result.ShouldBe(new HashSet<(long, long, long)> { (0, 0, 1), (0, 0, 3) });
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/history");
        request.RequestUri.Query.ShouldBe("?page=1&pageSize=500&sortDirection=descending");
    }

    [Fact]
    public async Task GetRecentlyGrabbedEpisodesAsync_FullPageOfRecentRows_ReadsNextPage()
    {
        // Arrange
        DateTimeOffset since = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            object[] records = req.RequestUri!.Query.StartsWith("?page=1")
                ? Enumerable.Range(1, 500).Select(i => (object)new { episodeId = i, eventType = "grabbed", date = "2026-10-31T10:00:00" }).ToArray()
                : [new { episodeId = 900, eventType = "grabbed", date = "2026-10-30T10:00:00" }];
            return Task.FromResult(JsonResponse(new { records }));
        });

        // Act
        HashSet<(long SeriesId, long SeasonNumber, long EpisodeId)> result =
            await _client.GetRecentlyGrabbedEpisodesAsync(_arrInstance, since);

        // Assert
        result.Count.ShouldBe(501);
        result.ShouldContain((0, 0, 900));
        _httpMessageHandler.CapturedRequests.Count.ShouldBe(2);
    }

    #endregion

    #region Helpers

    private static HttpResponseMessage JsonResponse<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    #endregion
}
