using System.Net;
using System.Text;
using System.Text.Json;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.ManualImport;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Entities.Radarr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class WhisparrV3ClientTests
{
    private readonly ILogger<WhisparrV3Client> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStriker _striker;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly WhisparrV3Client _client;
    private readonly ArrInstance _arrInstance;

    public WhisparrV3ClientTests()
    {
        _logger = Substitute.For<ILogger<WhisparrV3Client>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        HttpClient httpClient = new(_httpMessageHandler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new WhisparrV3Client(
            _logger,
            _httpClientFactory,
            _striker,
            _dryRunInterceptor
        );
        _arrInstance = new ArrInstance
        {
            Name = "whisparr",
            Url = new Uri("http://localhost:6969/"),
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

    #region Queue URL overrides

    [Fact]
    public async Task GetQueueItemsAsync_BuildsWhisparrSpecificQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new QueueListResponse { TotalRecords = 0, Records = Array.Empty<QueueRecord>() })));

        // Act
        await _client.GetQueueItemsAsync(_arrInstance, 4);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/queue");
        request.RequestUri.Query.ShouldBe("?page=4&pageSize=200&includeUnknownMovieItems=true&includeMovie=true");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_UsesV3QueuePath()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.DeleteQueueItemAsync(_arrInstance, BuildRecord(55), removeFromClient: true, changeCategory: false, DeleteReason.Stalled);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/queue/55");
    }

    [Fact]
    public async Task HealthCheckAsync_UsesV3SystemStatus()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.HealthCheckAsync(_arrInstance);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/system/status");
    }

    #endregion

    #region SearchItemsAsync

    [Fact]
    public async Task SearchItemsAsync_NullItems_ReturnsEmpty()
    {
        // Act
        List<long> ids = await _client.SearchItemsAsync(_arrInstance, null);

        // Assert
        ids.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchItemsAsync_EmptyItems_ReturnsEmpty()
    {
        // Act
        List<long> ids = await _client.SearchItemsAsync(_arrInstance, new HashSet<SearchItem>());

        // Assert
        ids.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchItemsAsync_PostsMoviesSearchCommandWithAllIds()
    {
        // Arrange: GET /movie/{id} calls build the log context, return null so it bails out
        _httpMessageHandler.SetupResponse((req, _) => Task.FromResult(JsonNullResponse()));
        HashSet<SearchItem> items = new()
        {
            new SearchItem { Id = 10 },
            new SearchItem { Id = 20 },
        };

        // Act
        List<long> ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBeEmpty();
        HttpRequestMessage post = _httpMessageHandler.CapturedRequests.Single(r => r.Method == HttpMethod.Post);
        post.RequestUri!.AbsolutePath.ShouldBe("/api/v3/command");
        string? body = _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(post)];
        body.ShouldNotBeNull();
        body!.ShouldContain("\"Name\":\"MoviesSearch\"", Case.Insensitive);
        body!.ShouldContain("\"MovieIds\":[10,20]", Case.Insensitive);
    }

    [Fact]
    public async Task SearchItemsAsync_ComputesLogContextByFetchingEachMovie()
    {
        // Arrange: GetMovie is called for every movie id to build the log context
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            if (req.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse(new Movie { Id = 10, Title = "Movie Title" }));
            }

            return Task.FromResult(JsonNullResponse());
        });
        HashSet<SearchItem> items = new() { new SearchItem { Id = 10 } };

        // Act
        await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        HttpRequestMessage get = _httpMessageHandler.CapturedRequests.Single(r => r.Method == HttpMethod.Get);
        get.RequestUri!.AbsolutePath.ShouldBe("/api/v3/movie/10");
    }

    #endregion

    [Fact]
    public void SupportsForceImport_IsTrue()
    {
        _client.SupportsForceImport.ShouldBeTrue();
    }

    [Fact]
    public void MapCandidate_TheMovieMatches_BuildsTheMoviePayload()
    {
        // Arrange
        QueueRecord record = new() { MovieId = 1, DownloadId = "HASH", Title = "movie" };
        ManualImportCandidate candidate = new()
        {
            Path = "/downloads/movie.mkv",
            DownloadId = "HASH",
            ReleaseGroup = "GROUP",
            MovieId = 1,
        };

        // Act
        ManualImportFile? file = _client.MapCandidate(record, candidate);

        // Assert
        file.ShouldNotBeNull();
        file.MovieId.ShouldBe(1);
        file.SeriesId.ShouldBeNull();
        file.ReleaseGroup.ShouldBe("GROUP");
    }

    [Fact]
    public void MapCandidate_NestedMovieMatches_BuildsTheMoviePayload()
    {
        // Arrange: an older build nests the movie
        QueueRecord record = new() { MovieId = 1, DownloadId = "HASH", Title = "movie" };
        ManualImportCandidate candidate = new() { Movie = new ManualImportRef { Id = 1 } };

        // Act, Assert
        _client.MapCandidate(record, candidate)!.MovieId.ShouldBe(1);
    }

    [Fact]
    public void MapCandidate_OtherMovie_ReturnsNull()
    {
        // Arrange
        QueueRecord record = new() { MovieId = 1, DownloadId = "HASH", Title = "movie" };

        // Act, Assert
        _client.MapCandidate(record, new ManualImportCandidate { MovieId = 2 }).ShouldBeNull();
    }

    [Fact]
    public void MapCandidate_NoMovieId_ReturnsNull()
    {
        // Arrange
        QueueRecord record = new() { MovieId = 1, DownloadId = "HASH", Title = "movie" };

        // Act, Assert
        _client.MapCandidate(record, new ManualImportCandidate()).ShouldBeNull();
    }

    [Fact]
    public void HasContentId_NoMovieId_IsFalse()
    {
        _client.HasContentId(new QueueRecord { DownloadId = "HASH", Title = "movie" }).ShouldBeFalse();
    }

    #region Helpers

    private static QueueRecord BuildRecord(long id) => new()
    {
        Id = id,
        Title = $"item-{id}",
        DownloadId = id.ToString(),
        Protocol = "torrent",
    };

    private static HttpResponseMessage JsonResponse<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage JsonNullResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("null", Encoding.UTF8, "application/json"),
    };

    #endregion
}
