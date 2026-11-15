using System.Net;
using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientAddTorrentTests
{
    private static readonly Uri MagnetUri = new("magnet:?xt=urn:btih:8c212779b4abde7c6bc608063a0d008b7e40ce32");

    [Fact]
    public async Task AddTorrentsAsync_UrlsRequest_FailsBody_Throws()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/add", "Fails.");

        QBittorrentClientRequestException exception = await Should.ThrowAsync<QBittorrentClientRequestException>(
            () => server.Client.AddTorrentsAsync(new AddTorrentUrlsRequest(MagnetUri)));

        exception.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddTorrentsAsync_CombinedRequest_FailsBody_Throws()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/add", "Fails.");

        await Should.ThrowAsync<QBittorrentClientRequestException>(
            () => server.Client.AddTorrentsAsync(new AddTorrentsRequest([MagnetUri])));
    }

    [Fact]
    public async Task AddTorrentsAsync_FilesRequest_FailsBody_Throws()
    {
        string file = Path.GetTempFileName();
        try
        {
            using FakeQBittorrentServer server = new();
            server.Route("api/v2/torrents/add", "Fails.");

            await Should.ThrowAsync<QBittorrentClientRequestException>(
                () => server.Client.AddTorrentsAsync(new AddTorrentFilesRequest(file)));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("Ok.")]
    [InlineData("")]
    public async Task AddTorrentsAsync_OkOrEmptyBody_Succeeds(string body)
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/add", body);

        await Should.NotThrowAsync(() => server.Client.AddTorrentsAsync(new AddTorrentUrlsRequest(MagnetUri)));
        await Should.NotThrowAsync(() => server.Client.AddTorrentsAsync(new AddTorrentsRequest([MagnetUri])));
    }
}
