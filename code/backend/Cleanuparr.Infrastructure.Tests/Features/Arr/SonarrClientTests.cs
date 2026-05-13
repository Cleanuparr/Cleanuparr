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
using Newtonsoft.Json;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class SonarrClientTests
{
    private readonly IStriker _striker;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly SonarrClient _client;
    private readonly ArrInstance _arrInstance;

    public SonarrClientTests()
    {
        var logger = Substitute.For<ILogger<SonarrClient>>();
        _striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        var httpClient = new HttpClient(_httpMessageHandler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new SonarrClient(logger, httpClientFactory, _striker, _dryRunInterceptor);
        _arrInstance = new ArrInstance
        {
            Name = "sonarr",
            Url = new Uri("http://localhost:8989/"),
            ApiKey = "api-key",
        };

        _dryRunInterceptor.IsDryRunEnabled().Returns(false);
        _dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .Returns(async ci =>
            {
                var del = ci.Arg<Delegate>();
                var args = (object[])ci[1];
                var task = (Task<HttpResponseMessage>)del.DynamicInvoke(args)!;
                return await task;
            });
    }

    #region Queue URL overrides (via GetQueueItemsAsync / DeleteQueueItemAsync / HealthCheckAsync)

    [Fact]
    public async Task GetQueueItemsAsync_BuildsSonarrSpecificQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new QueueListResponse { TotalRecords = 0, Records = Array.Empty<QueueRecord>() })));

        // Act
        await _client.GetQueueItemsAsync(_arrInstance, 1);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/queue");
        request.RequestUri.Query.ShouldBe("?page=1&pageSize=200&includeUnknownSeriesItems=true&includeSeries=true&includeEpisode=true");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_UsesV3QueuePath()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.DeleteQueueItemAsync(_arrInstance, BuildRecord(123), removeFromClient: true, changeCategory: false, DeleteReason.FailedImport);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/queue/123");
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
    public void HasContentId_BothSeriesAndEpisodeSet_ReturnsTrue()
    {
        var record = new QueueRecord { Id = 1, Title = "t", DownloadId = "h", Protocol = "torrent", SeriesId = 5, EpisodeId = 9 };
        _client.HasContentId(record).ShouldBeTrue();
    }

    [Fact]
    public void HasContentId_SeriesIdZero_ReturnsFalse()
    {
        var record = new QueueRecord { Id = 1, Title = "t", DownloadId = "h", Protocol = "torrent", SeriesId = 0, EpisodeId = 9 };
        _client.HasContentId(record).ShouldBeFalse();
    }

    [Fact]
    public void HasContentId_EpisodeIdZero_ReturnsFalse()
    {
        var record = new QueueRecord { Id = 1, Title = "t", DownloadId = "h", Protocol = "torrent", SeriesId = 5, EpisodeId = 0 };
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
    public async Task SearchItemsAsync_SeriesSearch_PostsSeriesCommandToCommandEndpoint()
    {
        // Arrange
        RouteResponses(commandIdForPost: 42);
        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 100, SeriesId = 100, SearchType = SeriesSearchType.Series },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBe(new long[] { 42 });
        var post = _httpMessageHandler.CapturedRequests.First(r => r.Method == HttpMethod.Post);
        post.RequestUri!.AbsolutePath.ShouldBe("/api/v3/command");
        var body = _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(post)];
        body.ShouldNotBeNull();
        body!.ShouldContain("\"name\":\"SeriesSearch\"", Case.Insensitive);
        body!.ShouldContain("\"seriesId\":100", Case.Insensitive);
    }

    [Fact]
    public async Task SearchItemsAsync_SeasonSearch_PostsSeasonCommandWithSeriesAndSeason()
    {
        // Arrange
        RouteResponses(commandIdForPost: 7);
        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 3, SeriesId = 100, SearchType = SeriesSearchType.Season },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBe(new long[] { 7 });
        var post = _httpMessageHandler.CapturedRequests.First(r => r.Method == HttpMethod.Post);
        var body = _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(post)];
        body.ShouldNotBeNull();
        body!.ShouldContain("\"name\":\"SeasonSearch\"", Case.Insensitive);
        body!.ShouldContain("\"seriesId\":100", Case.Insensitive);
        body!.ShouldContain("\"seasonNumber\":3", Case.Insensitive);
    }

    [Fact]
    public async Task SearchItemsAsync_MultipleEpisodes_BundlesIntoSingleCommand()
    {
        // Arrange
        RouteResponses(commandIdForPost: 99);
        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 1, SeriesId = 10, SearchType = SeriesSearchType.Episode },
            new SeriesSearchItem { Id = 2, SeriesId = 10, SearchType = SeriesSearchType.Episode },
            new SeriesSearchItem { Id = 3, SeriesId = 10, SearchType = SeriesSearchType.Episode },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBe(new long[] { 99 });
        var posts = _httpMessageHandler.CapturedRequests.Where(r => r.Method == HttpMethod.Post).ToList();
        posts.Count.ShouldBe(1);
        var bodyIndex = _httpMessageHandler.CapturedRequests.IndexOf(posts[0]);
        var body = _httpMessageHandler.CapturedRequestBodies[bodyIndex];
        body.ShouldNotBeNull();
        body!.ShouldContain("\"name\":\"EpisodeSearch\"", Case.Insensitive);
        body!.ShouldContain("\"episodeIds\":[1,2,3]", Case.Insensitive);
    }

    [Fact]
    public async Task SearchItemsAsync_DryRun_ReturnsEmptyAndDoesNotPost()
    {
        // Arrange — interceptor returns null on dry run
        _dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Delegate>(), Arg.Any<object[]>())
            .Returns((HttpResponseMessage?)null);
        // Set up GETs that ComputeCommandLogContext might fire (series lookup)
        _httpMessageHandler.SetupResponse((req, _) => Task.FromResult(JsonNullResponse()));

        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 5, SeriesId = 5, SearchType = SeriesSearchType.Series },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldNotContain(r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task SearchItemsAsync_ServerErrorOnPost_ThrowsAndLogsError()
    {
        // Arrange — interceptor passes through; GET log-context lookups return null body; POST 500
        _httpMessageHandler.SetupResponse((req, _) =>
        {
            if (req.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
            return Task.FromResult(JsonNullResponse());
        });

        var items = new HashSet<SearchItem>
        {
            new SeriesSearchItem { Id = 5, SeriesId = 5, SearchType = SeriesSearchType.Series },
        };

        // Act / Assert
        await Should.ThrowAsync<HttpRequestException>(() => _client.SearchItemsAsync(_arrInstance, items));
    }

    #endregion

    #region GetAllSeriesAsync / GetAllTagsAsync / GetEpisodes / EpisodeFiles / QualityProfiles / Scores

    [Fact]
    public async Task GetAllSeriesAsync_BuildsCorrectUriAndDeserializesList()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new SearchableSeries { Id = 1, Title = "Show", QualityProfileId = 2, Tags = new List<long>() },
        })));

        // Act
        var result = await _client.GetAllSeriesAsync(_arrInstance);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Title.ShouldBe("Show");
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/series");
        request.Headers.GetValues("x-api-key").ShouldHaveSingleItem().ShouldBe("api-key");
    }

    [Fact]
    public async Task GetAllSeriesAsync_NullBody_ReturnsEmpty()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        // Act
        var result = await _client.GetAllSeriesAsync(_arrInstance);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllTagsAsync_DeserializesListAndUsesV3Path()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new Tag { Id = 1, Label = "Anime" },
            new Tag { Id = 2, Label = "HD" },
        })));

        // Act
        var tags = await _client.GetAllTagsAsync(_arrInstance);

        // Assert
        tags.Count.ShouldBe(2);
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/tag");
    }

    [Fact]
    public async Task GetEpisodesAsync_BuildsSeriesIdQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(Array.Empty<SearchableEpisode>())));

        // Act
        await _client.GetEpisodesAsync(_arrInstance, seriesId: 42);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/episode");
        request.RequestUri.Query.ShouldBe("?seriesId=42");
    }

    [Fact]
    public async Task GetEpisodeFilesAsync_BuildsSeriesIdQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(Array.Empty<ArrEpisodeFile>())));

        // Act
        await _client.GetEpisodeFilesAsync(_arrInstance, seriesId: 7);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/episodefile");
        request.RequestUri.Query.ShouldBe("?seriesId=7");
    }

    [Fact]
    public async Task GetQualityProfilesAsync_DeserializesList()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new ArrQualityProfile { Id = 1, Name = "HD", CutoffFormatScore = 100 },
        })));

        // Act
        var profiles = await _client.GetQualityProfilesAsync(_arrInstance);

        // Assert
        profiles.Count.ShouldBe(1);
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v3/qualityprofile");
    }

    [Fact]
    public async Task GetEpisodeFileScoresAsync_EmptyList_MakesNoRequests()
    {
        // Act
        var scores = await _client.GetEpisodeFileScoresAsync(_arrInstance, new List<long>());

        // Assert
        scores.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEpisodeFileScoresAsync_OverHundred_BatchesIntoMultipleRequests()
    {
        // Arrange — 150 ids should produce 2 batches (100 + 50)
        var ids = Enumerable.Range(1, 150).Select(i => (long)i).ToList();
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(Array.Empty<MediaFileScore>())));

        // Act
        await _client.GetEpisodeFileScoresAsync(_arrInstance, ids);

        // Assert
        _httpMessageHandler.CapturedRequests.Count.ShouldBe(2);
        _httpMessageHandler.CapturedRequests.ShouldAllBe(r => r.RequestUri!.AbsolutePath == "/api/v3/episodefile");
    }

    [Fact]
    public async Task GetEpisodeFileScoresAsync_MergesScoresFromResponses()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new MediaFileScore { Id = 1, CustomFormatScore = 50 },
            new MediaFileScore { Id = 2, CustomFormatScore = -30 },
        })));

        // Act
        var scores = await _client.GetEpisodeFileScoresAsync(_arrInstance, new List<long> { 1, 2 });

        // Assert
        scores.Count.ShouldBe(2);
        scores[1].ShouldBe(50);
        scores[2].ShouldBe(-30);
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
            // GET log-context calls (/series/{id}, /episode?...) return null so log context bails out
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
