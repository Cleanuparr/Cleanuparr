using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities.LazyLibrarian;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.LazyLibrarian;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.LazyLibrarian;

public class LazyLibrarianServiceTests
{
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly LazyLibrarianService _service;
    private readonly ArrInstance _instance;

    private readonly List<string> _loggedMessages = [];

    public LazyLibrarianServiceTests()
    {
        ILogger<LazyLibrarianService> logger = Substitute.For<ILogger<LazyLibrarianService>>();
        logger
            .When(x => x.Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()))
            .Do(ci => _loggedMessages.Add(ci.ArgAt<object>(2).ToString() ?? string.Empty));
        IDryRunInterceptor dryRunInterceptor = Substitute.For<IDryRunInterceptor>();
        _httpMessageHandler = new FakeHttpMessageHandler();

        HttpClient httpClient = new(_httpMessageHandler);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        dryRunInterceptor.IsDryRunEnabled().Returns(false);
        dryRunInterceptor
            .InterceptAsync<HttpResponseMessage>(Arg.Any<Func<Task<HttpResponseMessage>>>(), Arg.Any<string?>())
            .Returns(async ci => await ci.Arg<Func<Task<HttpResponseMessage>>>()());

        _service = new LazyLibrarianService(logger, httpClientFactory, dryRunInterceptor);
        _instance = new ArrInstance
        {
            Name = "lazylibrarian",
            Url = new Uri("http://localhost:5299/"),
            ApiKey = "api-key",
        };
    }

    private void RespondWith(string body)
    {
        _httpMessageHandler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));
    }

    private static string Row(
        string bookId = "OL7353617M",
        string downloadId = "HASH1",
        string status = "Snatched",
        string mode = "torznab",
        string auxInfo = "eBook",
        string origin = "new",
        string source = "QBITTORRENT",
        string title = "A Book"
    ) =>
        $$"""
        {"BookID":"{{bookId}}","NZBtitle":"{{title}}","DownloadID":"{{downloadId}}","Source":"{{source}}",
         "Status":"{{status}}","NZBmode":"{{mode}}","AuxInfo":"{{auxInfo}}","Origin":"{{origin}}"}
        """;

    #region GetQueueAsync

    [Theory]
    [InlineData("OL7353617M")]
    [InlineData("zyTCAlFPjgYC")]
    [InlineData("12345")]
    public async Task GetQueueAsync_KeepsTheBookIdAsText(string bookId)
    {
        // Arrange
        RespondWith($"[{Row(bookId: bookId)}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldHaveSingleItem().BookId.ShouldBe(bookId);
    }

    [Theory]
    [InlineData("torrent")]
    [InlineData("torznab")]
    [InlineData("magnet")]
    public async Task GetQueueAsync_AcceptsEveryTorrentMode(string mode)
    {
        // Arrange
        RespondWith($"[{Row(mode: mode)}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("nzb")]
    [InlineData("direct")]
    [InlineData("irc")]
    [InlineData("")]
    public async Task GetQueueAsync_RejectsANonTorrentMode(string mode)
    {
        // Arrange
        RespondWith($"[{Row(mode: mode)}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Processed")]
    [InlineData("Seeding")]
    [InlineData("Aborted")]
    [InlineData("Failed")]
    public async Task GetQueueAsync_OnlyAcceptsSnatched(string status)
    {
        // Arrange
        RespondWith($"[{Row(status: status)}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("QBITTORRENT")]
    [InlineData("TRANSMISSION")]
    [InlineData("DELUGEWEBUI")]
    [InlineData("DELUGERPC")]
    [InlineData("UTORRENT")]
    [InlineData("RTORRENT")]
    public async Task GetQueueAsync_AcceptsEveryTorrentClient(string source)
    {
        // Arrange
        RespondWith($"[{Row(source: source)}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldHaveSingleItem().Source.IsTorrentClient().ShouldBeTrue();
    }

    [Theory]
    [InlineData("BLACKHOLE")]
    [InlineData("SYNOLOGY_TOR")]
    [InlineData("DIRECT")]
    [InlineData("")]
    public async Task GetQueueAsync_RejectsASourceThatIsNotATorrentClient(string source)
    {
        // Arrange: a blackhole row keeps a torrent mode but its DownloadID is a path, not a hash.
        RespondWith($"[{Row(source: source)}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("2026-08-01")]
    [InlineData("comic")]
    [InlineData("")]
    public async Task GetQueueAsync_DropsAnythingThatIsNotABookLibrary(string auxInfo)
    {
        // Arrange
        RespondWith($"[{Row(auxInfo: auxInfo)}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("eBook", BookLibrary.EBook)]
    [InlineData("AudioBook", BookLibrary.AudioBook)]
    [InlineData("audiobook", BookLibrary.AudioBook)]
    public async Task GetQueueAsync_ReadsTheLibrary(string auxInfo, BookLibrary expected)
    {
        // Arrange
        RespondWith($"[{Row(auxInfo: auxInfo)}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldHaveSingleItem().Library.ShouldBe(expected);
    }

    [Fact]
    public async Task GetQueueAsync_DropsALegacyRowWithoutABook()
    {
        // Arrange
        RespondWith($"[{Row(bookId: "unknown")}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetQueueAsync_MarksEverySiblingAdoptedWhenOneRowIsAdopted()
    {
        // Arrange: one torrent backs the ebook and the audiobook, and only the audiobook reports adopted.
        RespondWith($"[{Row(auxInfo: "eBook", origin: "new")},{Row(auxInfo: "AudioBook", origin: "adopted")}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.Count.ShouldBe(2);
        items.ShouldAllBe(item => item.WasAdoptedByLazyLibrarian);
    }

    [Fact]
    public async Task GetQueueAsync_TreatsAnUnsetOriginAsAdopted()
    {
        // Arrange
        RespondWith($"[{Row(origin: "")}]");

        // Act
        IReadOnlyList<LazyLibrarianQueueItem> items = await _service.GetQueueAsync(_instance);

        // Assert
        items.ShouldHaveSingleItem().WasAdoptedByLazyLibrarian.ShouldBeTrue();
    }

    [Fact]
    public async Task GetQueueAsync_ThrowsOnTheApiErrorEnvelope()
    {
        // Arrange
        RespondWith("""{"Success":false,"Error":{"Code":403,"Message":"Incorrect API key"}}""");

        // Act + Assert
        Exception exception = await Should.ThrowAsync<Exception>(() => _service.GetQueueAsync(_instance));
        exception.Message.ShouldContain("Incorrect API key");
    }

    #endregion

    #region GetClaimedHashesAsync

    [Fact]
    public async Task GetClaimedHashesAsync_ClaimsMagazinesAndComics()
    {
        // Arrange: neither can be acted on, but both must stay out of the Download Cleaner's reach.
        RespondWith($"[{Row(auxInfo: "2026-08-01", bookId: "New Scientist", downloadId: "MAG")},"
                    + $"{Row(auxInfo: "comic", bookId: "12_5", downloadId: "COMIC")},"
                    + $"{Row(downloadId: "BOOK")}]");

        // Act
        IReadOnlyList<string> hashes = await _service.GetClaimedHashesAsync(_instance);

        // Assert
        hashes.ShouldBe(["MAG", "COMIC", "BOOK"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetClaimedHashesAsync_ClaimsALegacyRowWithoutABook()
    {
        // Arrange
        RespondWith($"[{Row(bookId: "unknown", downloadId: "LEGACY")}]");

        // Act
        IReadOnlyList<string> hashes = await _service.GetClaimedHashesAsync(_instance);

        // Assert
        hashes.ShouldBe(["LEGACY"]);
    }

    [Fact]
    public async Task GetClaimedHashesAsync_DoesNotClaimANonTorrentRow()
    {
        // Arrange
        RespondWith($"[{Row(mode: "nzb", downloadId: "NZB")},{Row(source: "BLACKHOLE", downloadId: "BH")}]");

        // Act
        IReadOnlyList<string> hashes = await _service.GetClaimedHashesAsync(_instance);

        // Assert
        hashes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetClaimedHashesAsync_ReturnsEachHashOnce()
    {
        // Arrange
        RespondWith($"[{Row(auxInfo: "eBook")},{Row(auxInfo: "AudioBook")}]");

        // Act
        IReadOnlyList<string> hashes = await _service.GetClaimedHashesAsync(_instance);

        // Assert
        hashes.ShouldBe(["HASH1"]);
    }

    #endregion

    #region api key

    private const string ApiKey = "api-key";

    [Fact]
    public async Task TheApiKeyNeverReachesALogOrAnException_OnAQueueFailure()
    {
        // Arrange: the key rides in the query string, so any logged Uri would leak it.
        _httpMessageHandler.SetupResponse(HttpStatusCode.InternalServerError);

        // Act
        Exception exception = await Should.ThrowAsync<Exception>(() => _service.GetQueueAsync(_instance));

        // Assert
        exception.ToString().ShouldNotContain(ApiKey);
        _loggedMessages.ShouldAllBe(message => !message.Contains(ApiKey));
    }

    [Fact]
    public async Task TheApiKeyNeverReachesALogOrAnException_OnARejectedCommand()
    {
        // Arrange
        RespondWith("""{"Success":false,"Error":{"Code":403,"Message":"Incorrect API key"}}""");

        // Act
        Exception exception = await Should.ThrowAsync<Exception>(
            () => _service.ResetItemAsync(_instance, CreateItem()));

        // Assert
        exception.ToString().ShouldNotContain(ApiKey);
        _loggedMessages.ShouldAllBe(message => !message.Contains(ApiKey));
    }

    [Fact]
    public async Task TheUpstreamBodyNeverReachesAnException()
    {
        // Arrange: a reverse proxy error page can echo the request URI, and the URI carries the key.
        RespondWith($"<html>error at /api?apikey={ApiKey}&cmd=queueBook</html>");

        // Act
        Exception exception = await Should.ThrowAsync<Exception>(
            () => _service.ResetItemAsync(_instance, CreateItem()));

        // Assert
        exception.Message.ShouldNotContain(ApiKey);
        exception.Message.ShouldNotContain("html");
    }

    private static LazyLibrarianQueueItem CreateItem() => new()
    {
        DownloadId = "HASH1",
        Title = "A Book",
        BookId = "OL7353617M",
        Library = BookLibrary.EBook,
        Source = LazyLibrarianSource.QBittorrent,
        Origin = LazyLibrarianOrigin.New,
    };

    #endregion
}
