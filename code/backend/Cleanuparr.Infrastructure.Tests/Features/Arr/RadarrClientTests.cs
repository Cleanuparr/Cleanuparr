using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class RadarrClientTests
{
    private readonly IStriker _striker;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly RadarrClient _client;
    private readonly ArrInstance _arrInstance;

    public RadarrClientTests()
    {
        var logger = Substitute.For<ILogger<ArrClient>>();
        _striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        var httpClient = new HttpClient(_httpMessageHandler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new RadarrClient(logger, httpClientFactory, _striker, _dryRunInterceptor);
        _arrInstance = new ArrInstance
        {
            Name = "radarr",
            Url = new Uri("http://localhost:7878/"),
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
    public async Task GetQueueItemsAsync_BuildsRadarrSpecificQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new QueueListResponse { TotalRecords = 0, Records = Array.Empty<QueueRecord>() })));

        // Act
        await _client.GetQueueItemsAsync(_arrInstance, 4);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
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
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
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
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/system/status");
    }

    #endregion

    #region HasContentId

    [Fact]
    public void HasContentId_MovieIdSet_ReturnsTrue()
    {
        var record = new QueueRecord { Id = 1, Title = "t", DownloadId = "h", Protocol = "torrent", MovieId = 5 };
        _client.HasContentId(record).ShouldBeTrue();
    }

    [Fact]
    public void HasContentId_MovieIdZero_ReturnsFalse()
    {
        var record = new QueueRecord { Id = 1, Title = "t", DownloadId = "h", Protocol = "torrent", MovieId = 0 };
        _client.HasContentId(record).ShouldBeFalse();
    }

    #endregion

    #region SearchItemsAsync

    [Fact]
    public async Task SearchItemsAsync_NullItems_ReturnsEmpty()
    {
        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, null);

        // Assert
        ids.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchItemsAsync_EmptyItems_ReturnsEmpty()
    {
        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, new HashSet<SearchItem>());

        // Assert
        ids.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchItemsAsync_PostsMoviesSearchCommandWithAllIds()
    {
        // Arrange
        RouteResponses(commandIdForPost: 88);
        var items = new HashSet<SearchItem>
        {
            new SearchItem { Id = 10 },
            new SearchItem { Id = 20 },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBe(new long[] { 88 });
        var post = _httpMessageHandler.CapturedRequests.Single(r => r.Method == HttpMethod.Post);
        post.RequestUri!.AbsolutePath.ShouldBe("/api/v3/command");
        var body = _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(post)];
        body.ShouldNotBeNull();
        body!.ShouldContain("\"Name\":\"MoviesSearch\"", Case.Insensitive);
        body!.ShouldContain("\"MovieIds\":[10,20]", Case.Insensitive);
    }

    [Fact]
    public async Task SearchItemsAsync_NullCommandResponseFromDryRun_ReturnsEmpty()
    {
        // Arrange — interceptor returns null on dry-run
        _dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Func<Task<HttpResponseMessage>>>(), Arg.Any<string?>())
            .Returns((HttpResponseMessage?)null);
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        var items = new HashSet<SearchItem> { new() { Id = 5 } };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchItemsAsync_ServerError_Throws()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
            return Task.FromResult(JsonNullResponse());
        });

        var items = new HashSet<SearchItem> { new() { Id = 5 } };

        // Act / Assert
        await Should.ThrowAsync<HttpRequestException>(() => _client.SearchItemsAsync(_arrInstance, items));
    }

    [Fact]
    public async Task SearchItemsAsync_NoCommandIdInResponse_ReturnsEmpty()
    {
        // Arrange — POST returns 200 with body that has no id (treated as null id)
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                });
            }
            return Task.FromResult(JsonNullResponse());
        });

        var items = new HashSet<SearchItem> { new() { Id = 5 } };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBeEmpty();
    }

    #endregion

    #region GetAllMoviesAsync / GetAllTagsAsync / GetQualityProfilesAsync / GetMovieFileScoresAsync

    [Fact]
    public async Task GetAllMoviesAsync_DeserializesList()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new SearchableMovie { Id = 1, Title = "Movie", QualityProfileId = 1 },
        })));

        // Act
        var movies = await _client.GetAllMoviesAsync(_arrInstance);

        // Assert
        movies.Count.ShouldBe(1);
        movies[0].Title.ShouldBe("Movie");
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/movie");
    }

    [Fact]
    public async Task GetAllMoviesAsync_NullBody_ReturnsEmpty()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        // Act
        var movies = await _client.GetAllMoviesAsync(_arrInstance);

        // Assert
        movies.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllTagsAsync_DeserializesList()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new Tag { Id = 1, Label = "4K" },
        })));

        // Act
        var tags = await _client.GetAllTagsAsync(_arrInstance);

        // Assert
        tags.Count.ShouldBe(1);
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/tag");
    }

    [Fact]
    public async Task GetQualityProfilesAsync_DeserializesList()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new ArrQualityProfile { Id = 1, Name = "UHD", CutoffFormatScore = 100 },
        })));

        // Act
        var profiles = await _client.GetQualityProfilesAsync(_arrInstance);

        // Assert
        profiles.Count.ShouldBe(1);
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/qualityprofile");
    }

    [Fact]
    public async Task GetMovieFileScoresAsync_EmptyList_MakesNoRequests()
    {
        // Act
        var scores = await _client.GetMovieFileScoresAsync(_arrInstance, new List<long>());

        // Assert
        scores.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMovieFileScoresAsync_OverHundred_BatchesIntoMultipleRequests()
    {
        // Arrange — 250 ids should produce 3 batches (100 + 100 + 50)
        var ids = Enumerable.Range(1, 250).Select(i => (long)i).ToList();
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(Array.Empty<MediaFileScore>())));

        // Act
        await _client.GetMovieFileScoresAsync(_arrInstance, ids);

        // Assert
        _httpMessageHandler.CapturedRequests.Count.ShouldBe(3);
        _httpMessageHandler.CapturedRequests.ShouldAllBe(r => r.RequestUri!.AbsolutePath == "/api/v3/moviefile");
    }

    [Fact]
    public async Task GetMovieFileScoresAsync_MergesScoresFromResponses()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new MediaFileScore { Id = 11, CustomFormatScore = 200 },
            new MediaFileScore { Id = 12, CustomFormatScore = -50 },
        })));

        // Act
        var scores = await _client.GetMovieFileScoresAsync(_arrInstance, new List<long> { 11, 12 });

        // Assert
        scores.Count.ShouldBe(2);
        scores[11].ShouldBe(200);
        scores[12].ShouldBe(-50);
    }

    #endregion

    #region Helpers

    private void RouteResponses(long commandIdForPost)
    {
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/command"))
            {
                return Task.FromResult(JsonResponse(new { id = commandIdForPost }));
            }
            // GET log-context calls (/movie/{id}) — return null so log context bails out
            return Task.FromResult(JsonNullResponse());
        });
    }

    private static QueueRecord BuildRecord(long id) => new()
    {
        Id = id,
        Title = $"item-{id}",
        DownloadId = id.ToString(),
        Protocol = "torrent",
    };

    private static HttpResponseMessage JsonResponse<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage JsonNullResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("null", Encoding.UTF8, "application/json"),
    };

    #endregion
}
