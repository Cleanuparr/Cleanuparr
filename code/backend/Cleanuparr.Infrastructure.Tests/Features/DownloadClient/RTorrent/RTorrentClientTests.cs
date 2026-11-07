using System.Net;
using System.Xml.Linq;
using Cleanuparr.Domain.Entities.RTorrent.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.DownloadClient.RTorrent;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.RTorrent;

public class RTorrentClientTests
{
    private static (RTorrentClient client, FakeHttpMessageHandler handler) CreateClient(string responseXml)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseXml)
        }));

        DownloadClientConfig config = new()
        {
            Name = "rtorrent",
            TypeName = DownloadClientTypeName.rTorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
        };

        return (new RTorrentClient(config, new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task StopTorrentAsync_CallsDStopWithTheHash()
    {
        const string response = """
            <?xml version="1.0" encoding="UTF-8"?>
            <methodResponse><params><param><value><i4>0</i4></value></param></params></methodResponse>
            """;
        (RTorrentClient client, FakeHttpMessageHandler handler) = CreateClient(response);

        await client.StopTorrentAsync("HASH123");

        XDocument body = XDocument.Parse(handler.CapturedRequestBodies[0]!);
        body.Root!.Element("methodName")!.Value.ShouldBe("d.stop");
        body.Descendants("string").Single().Value.ShouldBe("HASH123");
    }

    [Fact]
    public async Task StopTorrentAsync_WhenRTorrentFaults_Throws()
    {
        const string response = """
            <?xml version="1.0" encoding="UTF-8"?>
            <methodResponse><fault><value><struct>
              <member><name>faultCode</name><value><i4>-501</i4></value></member>
              <member><name>faultString</name><value><string>Could not find info-hash.</string></value></member>
            </struct></value></fault></methodResponse>
            """;
        (RTorrentClient client, _) = CreateClient(response);

        await Should.ThrowAsync<Exception>(() => client.StopTorrentAsync("HASH123"));
    }

    private static string TorrentRowsResponse(params string[] cells)
    {
        string values = string.Concat(cells);
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <methodResponse><params><param><value><array><data>
            <value><array><data>{values}</data></array></value>
            </data></array></value></param></params></methodResponse>
            """;
    }

    private static string[] TorrentCells(int isActive = 0, int isMultiFile = 0)
    {
        return
        [
            "<value><string>HASH1</string></value>",
            "<value><string>Torrent 1</string></value>",
            "<value><i8>1</i8></value>",
            "<value><i8>1000</i8></value>",
            "<value><i8>500</i8></value>",
            "<value><i8>0</i8></value>",
            "<value><i8>0</i8></value>",
            "<value><i8>1</i8></value>",
            "<value><i8>0</i8></value>",
            "<value><i8>0</i8></value>",
            "<value><string>tv</string></value>",
            "<value><string>/downloads/Torrent 1</string></value>",
            "<value><string>/downloads</string></value>",
            $"<value><i8>{isActive}</i8></value>",
            $"<value><i8>{isMultiFile}</i8></value>",
        ];
    }

    [Fact]
    public async Task GetAllTorrentsAsync_RequestsIsActive()
    {
        (RTorrentClient client, FakeHttpMessageHandler handler) = CreateClient(TorrentRowsResponse(TorrentCells()));

        await client.GetAllTorrentsAsync();

        handler.CapturedRequestBodies[0]!.ShouldContain("d.is_active=");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task GetAllTorrentsAsync_ParsesIsActive(int isActive)
    {
        (RTorrentClient client, _) = CreateClient(TorrentRowsResponse(TorrentCells(isActive: isActive)));

        List<RTorrentTorrent> torrents = await client.GetAllTorrentsAsync();

        torrents.Single().IsActive.ShouldBe(isActive);
    }

    [Fact]
    public async Task GetAllTorrentsAsync_RequestsIsMultiFile()
    {
        (RTorrentClient client, FakeHttpMessageHandler handler) = CreateClient(TorrentRowsResponse(TorrentCells()));

        await client.GetAllTorrentsAsync();

        handler.CapturedRequestBodies[0]!.ShouldContain("d.is_multi_file=");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task GetAllTorrentsAsync_ParsesIsMultiFile(int isMultiFile)
    {
        (RTorrentClient client, _) = CreateClient(TorrentRowsResponse(TorrentCells(isMultiFile: isMultiFile)));

        List<RTorrentTorrent> torrents = await client.GetAllTorrentsAsync();

        torrents.Single().IsMultiFile.ShouldBe(isMultiFile);
    }

    [Fact]
    public async Task GetAllTorrentsAsync_WhenRowLacksIsMultiFile_Throws()
    {
        (RTorrentClient client, _) = CreateClient(TorrentRowsResponse(TorrentCells()[..^1]));

        await Should.ThrowAsync<RTorrentClientException>(() => client.GetAllTorrentsAsync());
    }
}
