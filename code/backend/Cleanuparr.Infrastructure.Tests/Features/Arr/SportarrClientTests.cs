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
    public async Task SearchItemsAsync_ResponseWithoutTaskId_ReturnsNoIds()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new { id = "guid" })));
        HashSet<SearchItem> items = [new SeriesSearchItem { Id = 5, SeriesId = 10, SearchType = SeriesSearchType.Episode }];

        // Act
        List<long> ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBeEmpty();
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

    #region Helpers

    private static HttpResponseMessage JsonResponse<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    #endregion
}
