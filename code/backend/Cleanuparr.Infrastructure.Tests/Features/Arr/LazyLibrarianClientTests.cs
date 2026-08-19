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
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class LazyLibrarianClientTests
{
    private readonly IDryRunInterceptor _dryRunInterceptor;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly LazyLibrarianClient _client;
    private readonly ArrInstance _arrInstance;

    public LazyLibrarianClientTests()
    {
        ILogger<LazyLibrarianClient> logger = Substitute.For<ILogger<LazyLibrarianClient>>();
        IStriker striker = Substitute.For<IStriker>();
        _dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        HttpClient httpClient = new(_httpMessageHandler);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        _client = new LazyLibrarianClient(logger, httpClientFactory, striker, _dryRunInterceptor);
        _arrInstance = new ArrInstance
        {
            Name = "lazylibrarian",
            Url = new Uri("http://localhost:5299/"),
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

    #region GetQueueItemsAsync

    [Theory]
    [InlineData("OL7353617M")]
    [InlineData("zyTCAlFPjgYC")]
    [InlineData("12345")]
    public async Task GetQueueItemsAsync_KeepsTheBookIdAsText(string bookId)
    {
        StubHistory($$"""
            [{"BookID":"{{bookId}}","NZBtitle":"Author - Title","DownloadID":"HASH","Status":"Snatched","NZBmode":"torrent","Source":"QBITTORRENT","AuxInfo":"eBook","Origin":"new"}]
            """);

        QueueListResponse response = await _client.GetQueueItemsAsync(_arrInstance, 1);

        QueueRecord record = response.Records.ShouldHaveSingleItem();
        record.ContentId.ShouldBe(bookId);
        record.DownloadId.ShouldBe("HASH");
        record.Title.ShouldBe("Author - Title");
        record.Protocol.ShouldBe("torrent");
    }

    [Fact]
    public async Task GetQueueItemsAsync_SendsTheApiKeyAndCommandAsQueryParameters()
    {
        StubHistory("[]");

        await _client.GetQueueItemsAsync(_arrInstance, 1);

        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/api");
        request.RequestUri.Query.ShouldBe("?apikey=api-key&cmd=getHistory");
    }

    [Fact]
    public async Task GetQueueItemsAsync_SkipsTheLegacyRowWithNoBook()
    {
        StubHistory("""
            [{"BookID":"unknown","NZBtitle":"Legacy row","DownloadID":"HASH","Status":"Snatched","NZBmode":"torrent","AuxInfo":"eBook"}]
            """);

        QueueListResponse response = await _client.GetQueueItemsAsync(_arrInstance, 1);

        response.Records.ShouldBeEmpty();
        response.TotalRecords.ShouldBe(0);
    }

    [Fact]
    public async Task GetQueueItemsAsync_SkipsRowsThatAreNotSnatchedTorrents()
    {
        StubHistory("""
            [
              {"BookID":"OL1M","NZBtitle":"nzb row","DownloadID":"HASH1","Status":"Snatched","NZBmode":"nzb","AuxInfo":"eBook"},
              {"BookID":"OL2M","NZBtitle":"direct row","DownloadID":"HASH2","Status":"Snatched","NZBmode":"direct","AuxInfo":"eBook"},
              {"BookID":"OL3M","NZBtitle":"irc row","DownloadID":"HASH3","Status":"Snatched","NZBmode":"irc","AuxInfo":"eBook"},
              {"BookID":"OL4M","NZBtitle":"no mode","DownloadID":"HASH4","Status":"Snatched","NZBmode":"","AuxInfo":"eBook"},
              {"BookID":"OL5M","NZBtitle":"processed row","DownloadID":"HASH5","Status":"Processed","NZBmode":"torrent","AuxInfo":"eBook"},
              {"BookID":"OL6M","NZBtitle":"no download id","DownloadID":"","Status":"Snatched","NZBmode":"torrent","AuxInfo":"eBook"}
            ]
            """);

        QueueListResponse response = await _client.GetQueueItemsAsync(_arrInstance, 1);

        response.Records.ShouldBeEmpty();
    }

    // LazyLibrarian records the provider type.
    // All three reach a torrent client.
    [Theory]
    [InlineData("torrent")]
    [InlineData("torznab")]
    [InlineData("magnet")]
    public async Task GetQueueItemsAsync_AcceptsEveryTorrentMode(string mode)
    {
        StubHistory($$"""
            [{"BookID":"OL450063W","NZBtitle":"Author - Title","DownloadID":"HASH","Status":"Snatched","NZBmode":"{{mode}}","AuxInfo":"eBook"}]
            """);

        QueueListResponse response = await _client.GetQueueItemsAsync(_arrInstance, 1);

        QueueRecord record = response.Records.ShouldHaveSingleItem();
        record.ContentId.ShouldBe("OL450063W");
    }

    // A magazine row carries the magazine title.
    // A comic row carries an issue key.
    // The book commands accept neither.
    [Theory]
    [InlineData("comic")]
    [InlineData("2026-08")]
    [InlineData("")]
    public async Task GetQueueItemsAsync_SkipsRowsThatAreNotBooks(string auxInfo)
    {
        StubHistory($$"""
            [{"BookID":"New Scientist","NZBtitle":"New Scientist 2026-08","DownloadID":"HASH","Status":"Snatched","NZBmode":"torrent","AuxInfo":"{{auxInfo}}"}]
            """);

        QueueListResponse response = await _client.GetQueueItemsAsync(_arrInstance, 1);

        response.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetQueueItemsAsync_KeepsTheLibraryAndTheOrigin()
    {
        StubHistory("""
            [{"BookID":"OL1M","NZBtitle":"t","DownloadID":"HASH","Status":"Snatched","NZBmode":"torrent","AuxInfo":"AudioBook","Origin":"adopted"}]
            """);

        QueueRecord record = (await _client.GetQueueItemsAsync(_arrInstance, 1)).Records.ShouldHaveSingleItem();

        record.Library.ShouldBe("AudioBook");
        record.DownloadOrigin.ShouldBe("adopted");
    }

    [Fact]
    public async Task GetQueueItemsAsync_WhenTheApiRejectsTheKey_Throws()
    {
        StubBody("""{"Success": false, "Data": "", "Error": {"Code": 401, "Message": "Incorrect API key"}}""");

        Exception exception = await Should.ThrowAsync<Exception>(() => _client.GetQueueItemsAsync(_arrInstance, 1));

        exception.Message.ShouldContain("Incorrect API key");
    }

    [Fact]
    public async Task GetQueueItemsAsync_SecondPageIsEmptyAndSendsNoRequest()
    {
        QueueListResponse response = await _client.GetQueueItemsAsync(_arrInstance, 2);

        response.Records.ShouldBeEmpty();
        response.TotalRecords.ShouldBe(0);
        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    #endregion

    #region DeleteQueueItemAsync

    [Fact]
    public async Task DeleteQueueItemAsync_ResetsTheBookWithTheTextId()
    {
        StubOk();

        await _client.DeleteQueueItemAsync(
            _arrInstance, BuildRecord("OL7353617M"), removeFromClient: true, changeCategory: false, DeleteReason.Stalled);

        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/api");
        request.RequestUri.Query.ShouldBe("?apikey=api-key&cmd=queueBook&id=OL7353617M");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_ForAnAudioBook_AsksForTheAudioBookStatus()
    {
        StubOk();

        QueueRecord record = BuildRecord("OL7353617M") with { Library = "AudioBook" };
        await _client.DeleteQueueItemAsync(
            _arrInstance, record, removeFromClient: true, changeCategory: false, DeleteReason.Stalled);

        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.Query.ShouldBe("?apikey=api-key&cmd=queueBook&id=OL7353617M&type=AudioBook");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_WhenTheBookIdIsUnknownToLazyLibrarian_Throws()
    {
        StubBody("Invalid id: New Scientist");

        Exception exception = await Should.ThrowAsync<Exception>(() => _client.DeleteQueueItemAsync(
            _arrInstance, BuildRecord("New Scientist"), removeFromClient: true, changeCategory: false, DeleteReason.Stalled));

        exception.Message.ShouldContain("Invalid id");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_WithAReadOnlyKey_Throws()
    {
        StubBody("""{"Success": false, "Data": "", "Error": {"Code": 405, "Message": "Command: queueBook not available with read-only api access key"}}""");

        Exception exception = await Should.ThrowAsync<Exception>(() => _client.DeleteQueueItemAsync(
            _arrInstance, BuildRecord("OL1M"), removeFromClient: true, changeCategory: false, DeleteReason.Stalled));

        exception.Message.ShouldContain("read-only");
    }

    [Fact]
    public async Task DeleteQueueItemAsync_WithoutABookId_SendsNothing()
    {
        StubOk();

        await _client.DeleteQueueItemAsync(
            _arrInstance, BuildRecord(null), removeFromClient: true, changeCategory: false, DeleteReason.Stalled);

        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    #endregion

    #region ShouldRemoveFromQueue

    [Fact]
    public async Task ShouldRemoveFromQueue_IsAlwaysFalse()
    {
        bool result = await _client.ShouldRemoveFromQueue(
            InstanceType.LazyLibrarian, BuildRecord("OL1M"), isPrivateDownload: false, arrMaxStrikes: 3);

        result.ShouldBeFalse();
    }

    #endregion

    #region HasContentId

    [Theory]
    [InlineData("OL7353617M", true)]
    [InlineData("12345", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasContentId_FollowsTheTextId(string? contentId, bool expected)
    {
        _client.HasContentId(BuildRecord(contentId)).ShouldBe(expected);
    }

    #endregion

    #region SearchItemsAsync

    [Fact]
    public async Task SearchItemsAsync_SearchesWithTheTextId()
    {
        StubOk();
        HashSet<SearchItem> items = [new BookSearchItem { ContentId = "OL7353617M" }];

        List<long> ids = await _client.SearchItemsAsync(_arrInstance, items);

        ids.ShouldBeEmpty();
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.Query.ShouldBe("?apikey=api-key&cmd=searchBook&id=OL7353617M");
    }

    [Fact]
    public async Task SearchItemsAsync_WithoutABookSearchItem_SendsNothing()
    {
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);
        HashSet<SearchItem> items = [new SearchItem { Id = 42 }];

        await _client.SearchItemsAsync(_arrInstance, items);

        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchItemAsync_ReturnsZeroBecauseThereIsNoCommandToTrack()
    {
        StubOk();

        long commandId = await _client.SearchItemAsync(_arrInstance, new BookSearchItem { ContentId = "OL7353617M" });

        commandId.ShouldBe(0);
        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.Query.ShouldContain("id=OL7353617M");
    }

    [Fact]
    public async Task SearchItemsAsync_WithNoItems_SendsNothing()
    {
        _httpMessageHandler.SetupResponse(HttpStatusCode.OK);

        await _client.SearchItemsAsync(_arrInstance, null);

        _httpMessageHandler.CapturedRequests.ShouldBeEmpty();
    }

    #endregion

    #region HealthCheckAsync

    [Fact]
    public async Task HealthCheckAsync_AsksForTheVersion()
    {
        StubBody("""{"Success": true, "current_version": "276d79c1"}""");

        await _client.HealthCheckAsync(_arrInstance);

        HttpRequestMessage request = _httpMessageHandler.CapturedRequests.ShouldHaveSingleItem();
        request.RequestUri!.Query.ShouldBe("?apikey=api-key&cmd=getVersion");
    }

    [Fact]
    public async Task HealthCheckAsync_ServerError_Throws()
    {
        _httpMessageHandler.SetupResponse(HttpStatusCode.InternalServerError);

        await Should.ThrowAsync<HttpRequestException>(() => _client.HealthCheckAsync(_arrInstance));
    }

    // LazyLibrarian answers HTTP 200 for every API error.
    // The body is the only signal.
    [Theory]
    [InlineData(401, "Incorrect API key")]
    [InlineData(400, "Missing parameter: apikey")]
    [InlineData(501, "API not enabled")]
    public async Task HealthCheckAsync_WhenTheApiReportsAnError_Throws(int code, string message)
    {
        StubBody($$$"""{"Success": false, "Data": "", "Error": {"Code": {{{code}}}, "Message": "{{{message}}}"}}""");

        Exception exception = await Should.ThrowAsync<Exception>(() => _client.HealthCheckAsync(_arrInstance));

        exception.Message.ShouldContain(message);
    }

    #endregion

    private void StubOk()
    {
        StubBody("OK");
    }

    /// <summary>
    /// LazyLibrarian answers HTTP 200 for a rejected command.
    /// A test needs the body.
    /// </summary>
    private void StubBody(string body)
    {
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/html"),
        }));
    }

    private void StubHistory(string json)
    {
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));
    }

    private static QueueRecord BuildRecord(string? contentId) => new()
    {
        Title = "Author - Title",
        DownloadId = "HASH",
        Protocol = "torrent",
        ContentId = contentId,
    };
}
