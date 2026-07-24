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
using System.Text.Json;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class LidarrClientTests
{
    private readonly IStriker _striker;
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly LidarrClient _client;
    private readonly ArrInstance _arrInstance;

    public LidarrClientTests()
    {
        var logger = Substitute.For<ILogger<LidarrClient>>();
        _striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        var httpClient = new HttpClient(_httpMessageHandler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new LidarrClient(logger, httpClientFactory, _striker, _dryRunInterceptor);
        _arrInstance = new ArrInstance
        {
            Name = "lidarr",
            Url = new Uri("http://localhost:8686/"),
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
    public async Task GetQueueItemsAsync_BuildsLidarrSpecificQuery()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new QueueListResponse { TotalRecords = 0, Records = Array.Empty<QueueRecord>() })));

        // Act
        await _client.GetQueueItemsAsync(_arrInstance, 2);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/queue");
        request.RequestUri.Query.ShouldBe("?page=2&pageSize=200&includeUnknownArtistItems=true&includeArtist=true&includeAlbum=true");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_UsesV1QueuePath()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.DeleteQueueItemAsync(_arrInstance, BuildRecord(7), removeFromClient: false, changeCategory: false, DeleteReason.FailedImport);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/queue/7");
    }

    [Fact]
    public async Task HealthCheckAsync_UsesV1SystemStatus()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        // Act
        await _client.HealthCheckAsync(_arrInstance);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/system/status");
    }

    #endregion

    #region HasContentId

    [Fact]
    public void HasContentId_ArtistAndAlbumSet_ReturnsTrue()
    {
        var record = new QueueRecord { Id = 1, Title = "t", DownloadId = "h", Protocol = "torrent", ArtistId = 1, AlbumId = 2 };
        _client.HasContentId(record).ShouldBeTrue();
    }

    [Fact]
    public void HasContentId_ArtistZero_ReturnsFalse()
    {
        var record = new QueueRecord { Id = 1, Title = "t", DownloadId = "h", Protocol = "torrent", ArtistId = 0, AlbumId = 2 };
        _client.HasContentId(record).ShouldBeFalse();
    }

    [Fact]
    public void HasContentId_AlbumZero_ReturnsFalse()
    {
        var record = new QueueRecord { Id = 1, Title = "t", DownloadId = "h", Protocol = "torrent", ArtistId = 1, AlbumId = 0 };
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
    public async Task SearchItemsAsync_PostsAlbumSearchCommandWithAllIds_AndReturnsEmpty()
    {
        // Arrange — Lidarr's SearchItemsAsync always returns [] regardless of HTTP response
        RouteResponses(commandIdForPost: 11);
        var items = new HashSet<SearchItem>
        {
            new SearchItem { Id = 10 },
            new SearchItem { Id = 20 },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBeEmpty();
        var post = _httpMessageHandler.CapturedRequests.Single(r => r.Method == HttpMethod.Post);
        post.RequestUri!.AbsolutePath.ShouldBe("/api/v1/command");
        var body = _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(post)];
        body.ShouldNotBeNull();
        body!.ShouldContain("\"name\":\"AlbumSearch\"", Case.Insensitive);
        body!.ShouldContain("\"albumIds\":[10,20]", Case.Insensitive);
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

        var items = new HashSet<SearchItem> { new() { Id = 1 } };

        // Act / Assert
        await Should.ThrowAsync<HttpRequestException>(() => _client.SearchItemsAsync(_arrInstance, items));
    }

    [Fact]
    public async Task SearchItemsAsync_DryRun_DoesNotPost()
    {
        // Arrange — interceptor returns null
        _dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Func<Task<HttpResponseMessage>>>(), Arg.Any<string?>())
            .Returns((HttpResponseMessage?)null);
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        var items = new HashSet<SearchItem> { new() { Id = 1 } };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBeEmpty();
        _httpMessageHandler.CapturedRequests.ShouldNotContain(r => r.Method == HttpMethod.Post);
    }

    #endregion

    #region GetAllTagsAsync

    [Fact]
    public async Task GetAllTagsAsync_ThrowsNotImplemented()
    {
        // Lidarr/Readarr/Whisparr do not implement tag listing
        await Should.ThrowAsync<NotImplementedException>(() => _client.GetAllTagsAsync(_arrInstance));
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
            // GET log-context calls (/album?...) return null so log context bails out
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
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage JsonNullResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("null", Encoding.UTF8, "application/json"),
    };

    #endregion
}
