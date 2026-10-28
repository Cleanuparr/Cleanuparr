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

    #region GetCommandStatusAsync / GetCommandsAsync

    [Fact]
    public async Task GetCommandStatusAsync_UsesV1CommandPath()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new ArrCommandStatus(42, ArrCommandState.Completed, null))));

        // Act
        await _client.GetCommandStatusAsync(_arrInstance, 42);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/command/42");
    }

    [Fact]
    public async Task GetCommandsAsync_UsesV1CommandPath()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(
            new List<ArrCommandStatus> { new(1, ArrCommandState.Started, null) })));

        // Act
        await _client.GetCommandsAsync(_arrInstance);

        // Assert
        var request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/command");
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
    public async Task SearchItemsAsync_PostsAlbumSearchCommandWithAllIds_AndReturnsCommandId()
    {
        // Arrange
        RouteResponses(commandIdForPost: 11);
        var items = new HashSet<SearchItem>
        {
            new SearchItem { Id = 10 },
            new SearchItem { Id = 20 },
        };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBe(new long[] { 11 });
        var post = _httpMessageHandler.CapturedRequests.Single(r => r.Method == HttpMethod.Post);
        post.RequestUri!.AbsolutePath.ShouldBe("/api/v1/command");
        var body = _httpMessageHandler.CapturedRequestBodies[_httpMessageHandler.CapturedRequests.IndexOf(post)];
        body.ShouldNotBeNull();
        body!.ShouldContain("\"name\":\"AlbumSearch\"", Case.Insensitive);
        body!.ShouldContain("\"albumIds\":[10,20]", Case.Insensitive);
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

        var items = new HashSet<SearchItem> { new() { Id = 1 } };

        // Act
        var ids = await _client.SearchItemsAsync(_arrInstance, items);

        // Assert
        ids.ShouldBeEmpty();
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
    public async Task SearchItemAsync_ReturnsCommandIdInsteadOfThrowing()
    {
        // Arrange
        RouteResponses(commandIdForPost: 77);

        // Act
        var id = await _client.SearchItemAsync(_arrInstance, new SearchItem { Id = 1 });

        // Assert
        id.ShouldBe(77);
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
    public async Task GetAllTagsAsync_DeserializesListFromV1Path()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new Tag { Id = 3, Label = "flac" },
        })));

        // Act
        List<Tag> tags = await _client.GetAllTagsAsync(_arrInstance);

        // Assert
        tags.ShouldHaveSingleItem().Label.ShouldBe("flac");
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/tag");
    }

    [Fact]
    public async Task GetAllTagsAsync_NullBody_ReturnsEmpty()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        // Act
        List<Tag> tags = await _client.GetAllTagsAsync(_arrInstance);

        // Assert
        tags.ShouldBeEmpty();
    }

    #endregion

    #region StreamAllArtistsAsync / GetQualityProfilesAsync

    [Fact]
    public async Task StreamAllArtistsAsync_DeserializesStjPayloadWithOmittedNulls()
    {
        // Arrange
        const string body = """
            [{"id":7,"artistName":"Artist","monitored":true,"tags":[1,2],"qualityProfileId":4,"added":"2026-01-02T03:04:05Z","status":"continuing"},
             {"id":8,"artistName":"Bare","monitored":false}]
            """;
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse(body)));

        // Act
        List<SearchableArtist> artists = [];
        await foreach (SearchableArtist artist in _client.StreamAllArtistsAsync(_arrInstance))
        {
            artists.Add(artist);
        }

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/artist");
        artists.Count.ShouldBe(2);
        artists[0].Id.ShouldBe(7);
        artists[0].ArtistName.ShouldBe("Artist");
        artists[0].Monitored.ShouldBeTrue();
        artists[0].Tags.ShouldBe([1L, 2L]);
        artists[0].QualityProfileId.ShouldBe(4);
        artists[0].Added.ShouldBe(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        artists[0].Status.ShouldBe("continuing");
        artists[1].Added.ShouldBeNull();
        artists[1].Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task StreamAllArtistsAsync_NullBody_ReturnsEmpty()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        // Act
        List<SearchableArtist> artists = [];
        await foreach (SearchableArtist artist in _client.StreamAllArtistsAsync(_arrInstance))
        {
            artists.Add(artist);
        }

        // Assert
        artists.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetQualityProfilesAsync_DeserializesListFromV1Path()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new ArrQualityProfile { Id = 1, Name = "Lossless", UpgradeAllowed = true, CutoffFormatScore = 50 },
        })));

        // Act
        List<ArrQualityProfile> profiles = await _client.GetQualityProfilesAsync(_arrInstance);

        // Assert
        ArrQualityProfile profile = profiles.ShouldHaveSingleItem();
        profile.UpgradeAllowed.ShouldBeTrue();
        profile.CutoffFormatScore.ShouldBe(50);
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/qualityprofile");
    }

    [Fact]
    public async Task GetQualityProfilesAsync_NullBody_ReturnsEmpty()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        // Act
        List<ArrQualityProfile> profiles = await _client.GetQualityProfilesAsync(_arrInstance);

        // Assert
        profiles.ShouldBeEmpty();
    }

    #endregion

    #region GetAlbumsAsync / GetTrackFilesAsync

    [Fact]
    public async Task GetAlbumsAsync_BuildsArtistQueryAndDeserializesStjPayload()
    {
        // Arrange
        const string body = """
            [{"id":11,"title":"Album","artistId":7,"monitored":true,"releaseDate":"2020-05-06T00:00:00Z","lastSearchTime":"2026-02-03T04:05:06Z","statistics":{"trackFileCount":3,"trackCount":10,"totalTrackCount":12}},
             {"id":12,"title":"Bare","artistId":7,"monitored":false}]
            """;
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse(body)));

        // Act
        List<SearchableAlbum> albums = await _client.GetAlbumsAsync(_arrInstance, 7);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/album");
        request.RequestUri.Query.ShouldBe("?artistId=7");
        albums.Count.ShouldBe(2);
        albums[0].Id.ShouldBe(11);
        albums[0].Title.ShouldBe("Album");
        albums[0].ArtistId.ShouldBe(7);
        albums[0].Monitored.ShouldBeTrue();
        albums[0].ReleaseDate.ShouldBe(new DateTimeOffset(2020, 5, 6, 0, 0, 0, TimeSpan.Zero));
        albums[0].LastSearchTime.ShouldBe(new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero));
        albums[0].Statistics.ShouldNotBeNull().TrackFileCount.ShouldBe(3);
        albums[0].Statistics!.TrackCount.ShouldBe(10);
        albums[1].ReleaseDate.ShouldBeNull();
        albums[1].LastSearchTime.ShouldBeNull();
        albums[1].Statistics.ShouldBeNull();
    }

    [Fact]
    public async Task GetAlbumsAsync_NullBody_Throws()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _client.GetAlbumsAsync(_arrInstance, 7));
    }

    [Fact]
    public async Task GetAlbumsAsync_EmptyArray_ReturnsEmpty()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse("[]")));

        // Act
        List<SearchableAlbum> albums = await _client.GetAlbumsAsync(_arrInstance, 7);

        // Assert
        albums.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTrackFilesAsync_BuildsArtistQueryAndDeserializesScores()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new[]
        {
            new ArrTrackFile { Id = 21, AlbumId = 11, CustomFormatScore = -5 },
        })));

        // Act
        List<ArrTrackFile> files = await _client.GetTrackFilesAsync(_arrInstance, 7);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/trackfile");
        request.RequestUri.Query.ShouldBe("?artistId=7");
        ArrTrackFile file = files.ShouldHaveSingleItem();
        file.Id.ShouldBe(21);
        file.AlbumId.ShouldBe(11);
        file.CustomFormatScore.ShouldBe(-5);
    }

    [Fact]
    public async Task GetTrackFilesAsync_NullBody_ReturnsEmpty()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonNullResponse()));

        // Act
        List<ArrTrackFile> files = await _client.GetTrackFilesAsync(_arrInstance, 7);

        // Assert
        files.ShouldBeEmpty();
    }

    #endregion

    #region GetRecentlyGrabbedAlbumIdsAsync

    [Fact]
    public async Task GetRecentlyGrabbedAlbumIdsAsync_BuildsExpectedUri()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(Array.Empty<object>())));
        DateTimeOffset since = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        await _client.GetRecentlyGrabbedAlbumIdsAsync(_arrInstance, since);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/history/since");
        request.RequestUri.Query.ShouldBe("?date=2026-01-01T00%3A00%3A00.0000000Z&eventType=1");
    }

    [Fact]
    public async Task GetRecentlyGrabbedAlbumIdsAsync_DropsZeroAlbumIds()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse(
            """[{"albumId":1},{"albumId":0},{"artistId":5}]""")));

        // Act
        HashSet<long> ids = await _client.GetRecentlyGrabbedAlbumIdsAsync(_arrInstance, DateTimeOffset.UtcNow);

        // Assert
        ids.ShouldBe([1L]);
    }

    [Fact]
    public async Task GetRecentlyGrabbedAlbumIdsAsync_TheArrRefused_Throws()
    {
        // Arrange
        _httpMessageHandler.SetupResponse(HttpStatusCode.InternalServerError);

        // Act, Assert
        await Should.ThrowAsync<HttpRequestException>(
            () => _client.GetRecentlyGrabbedAlbumIdsAsync(_arrInstance, DateTimeOffset.UtcNow));
    }

    #endregion

    #region GetMissingAlbumsAsync / GetCutoffUnmetAlbumsAsync

    [Fact]
    public async Task GetMissingAlbumsAsync_BuildsExpectedUri()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new ArrPagedResponse<SearchableAlbum>())));

        // Act
        await _client.GetMissingAlbumsAsync(_arrInstance, monitored: true);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/wanted/missing");
        request.RequestUri.Query.ShouldBe("?page=1&pageSize=1000&monitored=true");
    }

    [Fact]
    public async Task GetCutoffUnmetAlbumsAsync_MonitoredFalse_BuildsExpectedUri()
    {
        // Arrange
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(JsonResponse(new ArrPagedResponse<SearchableAlbum>())));

        // Act
        await _client.GetCutoffUnmetAlbumsAsync(_arrInstance, monitored: false);

        // Assert
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/wanted/cutoff");
        request.RequestUri.Query.ShouldBe("?page=1&pageSize=1000&monitored=false");
    }

    [Fact]
    public async Task GetMissingAlbumsAsync_DeserializesPagedAlbums()
    {
        // Arrange
        const string body = """
            {"page":1,"pageSize":1000,"totalRecords":1,"records":[{"id":11,"title":"Album","artistId":7,"monitored":true,"releaseDate":"2020-05-06T00:00:00Z"}]}
            """;
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(RawJsonResponse(body)));

        // Act
        List<SearchableAlbum> albums = await _client.GetMissingAlbumsAsync(_arrInstance, monitored: true);

        // Assert
        SearchableAlbum album = albums.ShouldHaveSingleItem();
        album.Id.ShouldBe(11);
        album.ArtistId.ShouldBe(7);
        album.LastSearchTime.ShouldBeNull();
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

    private static HttpResponseMessage RawJsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage JsonNullResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("null", Encoding.UTF8, "application/json"),
    };

    #endregion
}
