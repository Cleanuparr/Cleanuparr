using System.Net;
using System.Text;
using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Infrastructure.Features.DownloadClient.QBittorrent;
using Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;
using Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using QBittorrent.Client;
using Shouldly;
using Xunit;
using TransmissionClient = Transmission.API.RPC.Client;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient;

/// <summary>
/// A failing torrent-list call must throw, never report zero torrents.
/// </summary>
public sealed class TorrentListErrorSurfacingTests
{
    private const string Html = "<html><body>Sign in to continue</body></html>";

    private static DownloadClientConfig Config(string host, DownloadClientTypeName typeName) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"Test {typeName}",
        TypeName = typeName,
        Type = DownloadClientType.Torrent,
        Enabled = true,
        Host = new Uri(host),
        Username = "admin",
        Password = "admin",
        UrlBase = string.Empty,
    };

    private static HttpClient Client(FakeHttpMessageHandler handler) => new(handler);

    private static HttpResponseMessage Respond(HttpStatusCode status, string body, string contentType = "text/plain") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };

    #region rTorrent

    private static RTorrentClient RTorrent(FakeHttpMessageHandler handler) =>
        new(Config("http://localhost/RPC2", DownloadClientTypeName.rTorrent), Client(handler));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RTorrent_ListCallReturnsErrorStatus_Throws(HttpStatusCode status)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(status, "failure")));

        await Should.ThrowAsync<Exception>(() => RTorrent(handler).GetAllTorrentsAsync());
    }

    [Fact]
    public async Task RTorrent_ListCallReturnsLoginHtml_Throws()
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, Html, "text/html")));

        await Should.ThrowAsync<Exception>(() => RTorrent(handler).GetAllTorrentsAsync());
    }

    [Fact]
    public async Task RTorrent_ListCallReturnsEmptyBody_Throws()
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, string.Empty)));

        await Should.ThrowAsync<Exception>(() => RTorrent(handler).GetAllTorrentsAsync());
    }

    [Fact]
    public async Task RTorrent_ListCallReturnsXmlRpcFault_Throws()
    {
        const string fault = """
            <?xml version="1.0"?>
            <methodResponse><fault><value><struct>
              <member><name>faultCode</name><value><i4>-501</i4></value></member>
              <member><name>faultString</name><value><string>Unsupported target type found.</string></value></member>
            </struct></value></fault></methodResponse>
            """;

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, fault, "text/xml")));

        Exception exception = await Should.ThrowAsync<Exception>(() => RTorrent(handler).GetAllTorrentsAsync());
        exception.Message.ShouldContain("Unsupported target type found.");
    }

    [Fact]
    public async Task RTorrent_ListCallReturnsNonArrayValue_Throws()
    {
        const string scalar = """
            <?xml version="1.0"?>
            <methodResponse><params><param><value><string>not a list</string></value></param></params></methodResponse>
            """;

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, scalar, "text/xml")));

        await Should.ThrowAsync<Exception>(() => RTorrent(handler).GetAllTorrentsAsync());
    }

    [Fact]
    public async Task RTorrent_ListCallReturnsRowWithMissingFields_Throws()
    {
        // 12 values where the client requests 13 fields.
        string values = string.Concat(Enumerable.Repeat("<value><string>x</string></value>", 12));
        string response = $"""
            <?xml version="1.0"?>
            <methodResponse><params><param><value><array><data>
              <value><array><data>{values}</data></array></value>
            </data></array></value></param></params></methodResponse>
            """;

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, response, "text/xml")));

        await Should.ThrowAsync<Exception>(() => RTorrent(handler).GetAllTorrentsAsync());
    }

    [Fact]
    public async Task RTorrent_ListCallReturnsEmptyArray_ReturnsEmptyWithoutThrowing()
    {
        const string empty = """
            <?xml version="1.0"?>
            <methodResponse><params><param><value><array><data/></array></value></param></params></methodResponse>
            """;

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, empty, "text/xml")));

        List<Cleanuparr.Domain.Entities.RTorrent.Response.RTorrentTorrent> torrents =
            await RTorrent(handler).GetAllTorrentsAsync();

        torrents.ShouldBeEmpty();
    }

    #endregion

    #region Deluge

    private static DelugeClient Deluge(FakeHttpMessageHandler handler) =>
        new(Config("http://localhost:8112", DownloadClientTypeName.Deluge), Client(handler));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Deluge_ListCallReturnsErrorStatus_Throws(HttpStatusCode status)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(status, "failure")));

        await Should.ThrowAsync<Exception>(() => Deluge(handler).GetStatusForAllTorrents());
    }

    [Fact]
    public async Task Deluge_ListCallReturnsLoginHtml_Throws()
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, Html, "text/html")));

        await Should.ThrowAsync<Exception>(() => Deluge(handler).GetStatusForAllTorrents());
    }

    [Fact]
    public async Task Deluge_ListCallReturnsEmptyBody_Throws()
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, string.Empty)));

        await Should.ThrowAsync<Exception>(() => Deluge(handler).GetStatusForAllTorrents());
    }

    [Fact]
    public async Task Deluge_ListCallReturnsRpcError_Throws()
    {
        const string rpcError = """
            {"result": null, "error": {"message": "Not connected to a daemon", "code": 1}, "id": 1}
            """;

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, rpcError, "application/json")));

        Exception exception = await Should.ThrowAsync<Exception>(() => Deluge(handler).GetStatusForAllTorrents());
        exception.Message.ShouldContain("Not connected to a daemon");
    }

    [Fact]
    public async Task Deluge_ListCallReturnsMismatchedRequestId_Throws()
    {
        const string desync = """
            {"result": {}, "error": null, "id": 99}
            """;

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, desync, "application/json")));

        await Should.ThrowAsync<Exception>(() => Deluge(handler).GetStatusForAllTorrents());
    }

    [Fact]
    public async Task Deluge_ListCallReturnsNullResult_ReturnsNull()
    {
        // A null RPC result carries no error, so the client hands back null.
        // DelugeServiceDC rejects it.
        const string nullResult = """
            {"result": null, "error": null, "id": 1}
            """;

        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, nullResult, "application/json")));

        List<Cleanuparr.Domain.Entities.Deluge.Response.DownloadStatus>? torrents =
            await Deluge(handler).GetStatusForAllTorrents();

        torrents.ShouldBeNull();
    }

    #endregion

    #region qBittorrent

    private static IQBittorrentClientWrapper QBit(FakeHttpMessageHandler handler) =>
        new QBittorrentClientWrapper(new QBittorrentClient(Client(handler), new Uri("http://localhost:8090")));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task QBit_ListCallReturnsErrorStatus_Throws(HttpStatusCode status)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(status, "failure")));

        await Should.ThrowAsync<Exception>(() => QBit(handler).GetTorrentListAsync(new TorrentListQuery()));
    }

    [Fact]
    public async Task QBit_ListCallReturnsLoginHtml_Throws()
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, Html, "text/html")));

        await Should.ThrowAsync<Exception>(() => QBit(handler).GetTorrentListAsync(new TorrentListQuery()));
    }

    [Fact]
    public async Task QBit_ListCallReturnsEmptyBody_ClientHandsBackNull()
    {
        // The library hands back null instead of throwing.
        // QBitServiceDC rejects the null itself.
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, string.Empty)));

        IReadOnlyList<TorrentInfo> list = await QBit(handler).GetTorrentListAsync(new TorrentListQuery());

        list.ShouldBeNull();
    }

    [Fact]
    public async Task QBit_ClientHandsBackNullList_ServiceThrows()
    {
        using QBitServiceFixture fixture = new();
        QBitService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetTorrentListAsync(Arg.Any<TorrentListQuery>()).Returns((IReadOnlyList<TorrentInfo>)null!);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllTorrentsLite());
    }

    [Fact]
    public async Task QBit_ReportsTorrentsWithoutHashes_Throws()
    {
        using QBitServiceFixture fixture = new();
        QBitService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetTorrentListAsync(Arg.Any<TorrentListQuery>())
            .Returns(new List<TorrentInfo> { new() });

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllTorrentsLite());
    }

    #endregion

    #region Transmission

    private static TransmissionClient Transmission(FakeHttpMessageHandler handler) =>
        new(Client(handler), "http://localhost:9091/transmission/rpc", login: "admin", password: "admin");

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Transmission_ListCallReturnsErrorStatus_Throws(HttpStatusCode status)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(status, "failure")));

        await Should.ThrowAsync<Exception>(() => Transmission(handler).TorrentGetAsync(["id", "hashString"]));
    }

    [Fact]
    public async Task Transmission_ListCallReturnsLoginHtml_Throws()
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, Html, "text/html")));

        await Should.ThrowAsync<Exception>(() => Transmission(handler).TorrentGetAsync(["id", "hashString"]));
    }

    [Fact]
    public async Task Transmission_ListCallReturnsEmptyBody_Throws()
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(HttpStatusCode.OK, string.Empty)));

        await Should.ThrowAsync<Exception>(() => Transmission(handler).TorrentGetAsync(["id", "hashString"]));
    }

    #endregion

    #region uTorrent

    private static UTorrentHttpService UTorrentHttp(FakeHttpMessageHandler handler) =>
        new(Client(handler),
            Config("http://localhost:8083", DownloadClientTypeName.uTorrent),
            Substitute.For<ILogger<UTorrentHttpService>>());

    private static UTorrentResponseParser UTorrentParser() =>
        new(Substitute.For<ILogger<UTorrentResponseParser>>());

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task UTorrent_ListCallReturnsErrorStatus_Throws(HttpStatusCode status)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(Respond(status, "failure")));

        await Should.ThrowAsync<Exception>(() => UTorrentHttp(handler)
            .SendRawRequestAsync(UTorrentRequestFactory.CreateTorrentListRequest(), "guid=abc"));
    }

    [Fact]
    public void UTorrent_ParserGetsLoginHtml_Throws()
    {
        Should.Throw<Exception>(() => UTorrentParser().ParseTorrentList(Html));
    }

    [Fact]
    public void UTorrent_ParserGetsEmptyBody_Throws()
    {
        Should.Throw<Exception>(() => UTorrentParser().ParseTorrentList(string.Empty));
    }

    [Fact]
    public void UTorrent_ParserGetsRowWithMissingFields_Throws()
    {
        // 26 fields where the parser requires 27.
        string row = string.Join(",", Enumerable.Repeat("\"x\"", 26));
        string json = $"{{\"build\":1,\"torrents\":[[{row}]]}}";

        Should.Throw<Exception>(() => UTorrentParser().ParseTorrentList(json));
    }

    [Fact]
    public void UTorrent_ParserGetsRowWithExactFieldCount_ParsesIt()
    {
        const string json = """
            {"build":1,"torrents":[[
              "ABC123", 201, "Some.Release", 1024, 1000, 1024, 512, 500, 0, 0, 0, "tv",
              0, 0, 0, 0, 65536, 1, 0, "", "", "Seeding", "", 1700000000, 1700000001, "", "/downloads"
            ]]}
            """;

        Cleanuparr.Domain.Entities.UTorrent.Response.TorrentListResponse response =
            UTorrentParser().ParseTorrentList(json);

        response.Torrents.Count.ShouldBe(1);
        response.Torrents[0].Hash.ShouldBe("ABC123");
        response.Torrents[0].SavePath.ShouldBe("/downloads");
    }

    [Fact]
    public void UTorrent_ParserGetsEmptyTorrentArray_ReturnsEmptyWithoutThrowing()
    {
        Cleanuparr.Domain.Entities.UTorrent.Response.TorrentListResponse response =
            UTorrentParser().ParseTorrentList("{\"build\":1,\"torrents\":[]}");

        response.Torrents.ShouldBeEmpty();
    }

    #endregion

    #region Zero-collapse guard

    // Rows whose hash did not bind used to be filtered away.
    // A list of only such rows became an empty list.

    [Fact]
    public async Task RTorrent_ReportsTorrentsWithoutHashes_Throws()
    {
        using RTorrentServiceFixture fixture = new();
        RTorrentService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetAllTorrentsAsync().Returns([
            new Cleanuparr.Domain.Entities.RTorrent.Response.RTorrentTorrent { Hash = "", Name = "no-hash" },
        ]);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllTorrentsLite());
    }

    [Fact]
    public async Task Deluge_ReportsTorrentsWithoutHashes_Throws()
    {
        using DelugeServiceFixture fixture = new();
        DelugeService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetStatusForAllTorrents().Returns([
            new Cleanuparr.Domain.Entities.Deluge.Response.DownloadStatus { Hash = "", Name = "no-hash" },
        ]);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllTorrentsLite());
    }

    [Fact]
    public async Task Deluge_ClientHandsBackNullList_ServiceThrows()
    {
        using DelugeServiceFixture fixture = new();
        DelugeService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetStatusForAllTorrents()
            .Returns((List<Cleanuparr.Domain.Entities.Deluge.Response.DownloadStatus>?)null);

        await Should.ThrowAsync<Cleanuparr.Domain.Exceptions.DelugeClientException>(() => sut.GetAllTorrentsLite());
    }

    [Fact]
    public async Task Transmission_ClientHandsBackNullList_ServiceThrows()
    {
        using TransmissionServiceFixture fixture = new();
        TransmissionService sut = fixture.CreateSut();
        fixture.ClientWrapper.TorrentGetAsync(Arg.Any<string[]>(), Arg.Any<string?>())
            .Returns((Transmission.API.RPC.Entity.TransmissionTorrents?)null);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllTorrentsLite());
    }

    [Fact]
    public async Task Transmission_ReportsTorrentsWithoutHashes_Throws()
    {
        using TransmissionServiceFixture fixture = new();
        TransmissionService sut = fixture.CreateSut();
        fixture.ClientWrapper.TorrentGetAsync(Arg.Any<string[]>(), Arg.Any<string?>())
            .Returns(new Transmission.API.RPC.Entity.TransmissionTorrents
            {
                Torrents = [new Transmission.API.RPC.Entity.TorrentInfo { HashString = "" }],
            });

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllTorrentsLite());
    }

    [Fact]
    public async Task UTorrent_ReportsTorrentsWithoutHashes_Throws()
    {
        using UTorrentServiceFixture fixture = new();
        UTorrentService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetTorrentsAsync().Returns([
            new Cleanuparr.Domain.Entities.UTorrent.Response.UTorrentItem { Hash = "", Name = "no-hash" },
        ]);

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetAllTorrentsLite());
    }

    #endregion

    #region Empty client

    // The counterpart to the guard above: a client holding nothing is not a faulty client.
    // Issue #746 came from reading these two states as one.

    [Fact]
    public async Task RTorrent_ReportsNoTorrents_ReturnsEmpty()
    {
        using RTorrentServiceFixture fixture = new();
        RTorrentService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetAllTorrentsAsync()
            .Returns(new List<Cleanuparr.Domain.Entities.RTorrent.Response.RTorrentTorrent>());

        List<ITorrentItemWrapper> torrents = await sut.GetAllTorrentsLite();

        torrents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deluge_ReportsNoTorrents_ReturnsEmpty()
    {
        using DelugeServiceFixture fixture = new();
        DelugeService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetStatusForAllTorrents()
            .Returns(new List<Cleanuparr.Domain.Entities.Deluge.Response.DownloadStatus>());

        List<ITorrentItemWrapper> torrents = await sut.GetAllTorrentsLite();

        torrents.ShouldBeEmpty();
    }

    [Fact]
    public async Task QBit_ReportsNoTorrents_ReturnsEmpty()
    {
        using QBitServiceFixture fixture = new();
        QBitService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetTorrentListAsync(Arg.Any<TorrentListQuery>())
            .Returns(new List<TorrentInfo>());

        List<ITorrentItemWrapper> torrents = await sut.GetAllTorrentsLite();

        torrents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Transmission_ReportsNoTorrents_ReturnsEmpty()
    {
        using TransmissionServiceFixture fixture = new();
        TransmissionService sut = fixture.CreateSut();
        fixture.ClientWrapper.TorrentGetAsync(Arg.Any<string[]>(), Arg.Any<string?>())
            .Returns(new Transmission.API.RPC.Entity.TransmissionTorrents { Torrents = [] });

        List<ITorrentItemWrapper> torrents = await sut.GetAllTorrentsLite();

        torrents.ShouldBeEmpty();
    }

    [Fact]
    public async Task UTorrent_ReportsNoTorrents_ReturnsEmpty()
    {
        using UTorrentServiceFixture fixture = new();
        UTorrentService sut = fixture.CreateSut();
        fixture.ClientWrapper.GetTorrentsAsync()
            .Returns(new List<Cleanuparr.Domain.Entities.UTorrent.Response.UTorrentItem>());

        List<ITorrentItemWrapper> torrents = await sut.GetAllTorrentsLite();

        torrents.ShouldBeEmpty();
    }

    #endregion
}
