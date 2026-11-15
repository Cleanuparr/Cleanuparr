using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientTorrentListFilterTests
{
    [Theory]
    [InlineData("2.11.0", TorrentListFilter.Paused, "stopped")]
    [InlineData("2.11.4", TorrentListFilter.Paused, "stopped")]
    [InlineData("2.11.4", TorrentListFilter.Resumed, "running")]
    [InlineData("2.10.4", TorrentListFilter.Paused, "paused")]
    [InlineData("2.10.4", TorrentListFilter.Resumed, "resumed")]
    [InlineData("2.11.4", TorrentListFilter.Completed, "completed")]
    [InlineData("2.11.4", TorrentListFilter.All, "all")]
    [InlineData("2.10.4", TorrentListFilter.Completed, "completed")]
    [InlineData("2.11.4", TorrentListFilter.StalledUploading, "stalled_uploading")]
    public async Task GetTorrentListAsync_Filter_MapsByApiVersion(string apiVersion, TorrentListFilter filter, string expected)
    {
        using FakeQBittorrentServer server = new(apiVersion);
        server.Route("api/v2/torrents/info", "[]");

        await server.Client.GetTorrentListAsync(new TorrentListQuery { Filter = filter });

        server.SingleApiRequest.Uri.Query.ShouldBe($"?filter={expected}&reverse=false");
    }
}
