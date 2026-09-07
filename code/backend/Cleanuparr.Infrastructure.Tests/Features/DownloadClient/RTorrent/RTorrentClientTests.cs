using System.Net;
using System.Xml.Linq;
using Cleanuparr.Domain.Enums;
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
}
