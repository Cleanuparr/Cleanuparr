using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientTorrentDetailsTests
{
    private const string Hash = "8c212779b4abde7c6bc608063a0d008b7e40ce32";

    [Fact]
    public async Task GetTorrentPropertiesAsync_RequestsHashAndParsesProperties()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/properties", Fixtures.Read("torrent-properties.json"));

        TorrentProperties? properties = await server.Client.GetTorrentPropertiesAsync(Hash);

        server.SingleApiRequest.Method.ShouldBe(HttpMethod.Get);
        server.SingleApiRequest.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/properties");
        server.SingleApiRequest.Uri.Query.ShouldBe($"?hash={Hash}");
        properties.ShouldNotBeNull();
        properties.SavePath.ShouldBe("/downloads");
        properties.ShareRatio.ShouldBe(1.25);
        properties.AdditionDate.ShouldBe(new DateTime(2023, 11, 14, 22, 13, 20));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task GetTorrentPropertiesAsync_ExposesIsPrivateInAdditionalData(string json, bool expected)
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/properties", $$"""{"save_path":"/downloads","is_private":{{json}}}""");

        TorrentProperties? properties = await server.Client.GetTorrentPropertiesAsync(Hash);

        properties!.AdditionalData.ShouldContainKey("is_private");
        properties.AdditionalData["is_private"].ToObject<bool>().ShouldBe(expected);
    }

    [Fact]
    public async Task GetTorrentPropertiesAsync_FixtureKeepsIsPrivateInAdditionalData()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/properties", Fixtures.Read("torrent-properties.json"));

        TorrentProperties? properties = await server.Client.GetTorrentPropertiesAsync(Hash);

        properties!.AdditionalData["is_private"].ToObject<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task GetTorrentPropertiesAsync_NotFound_ReturnsNull()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/properties", "", System.Net.HttpStatusCode.NotFound);

        (await server.Client.GetTorrentPropertiesAsync(Hash)).ShouldBeNull();
    }

    [Fact]
    public async Task GetTorrentContentsAsync_RequestsHashAndParsesFiles()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/files", Fixtures.Read("torrent-files.json"));

        IReadOnlyList<TorrentContent>? contents = await server.Client.GetTorrentContentsAsync(Hash);

        server.SingleApiRequest.Method.ShouldBe(HttpMethod.Get);
        server.SingleApiRequest.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/files");
        server.SingleApiRequest.Uri.Query.ShouldBe($"?hash={Hash}");
        contents.ShouldNotBeNull();
        contents.Count.ShouldBe(2);
        contents[0].Index.ShouldBe(0);
        contents[0].Name.ShouldBe("show/episode.mkv");
        contents[0].Priority.ShouldBe(TorrentContentPriority.Minimal);
        contents[1].Index.ShouldBe(1);
        contents[1].Name.ShouldBe("show/sample.exe");
        contents[1].Priority.ShouldBe(TorrentContentPriority.Skip);
    }

    [Fact]
    public async Task GetTorrentContentsAsync_NotFound_ReturnsNull()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/files", "", System.Net.HttpStatusCode.NotFound);

        (await server.Client.GetTorrentContentsAsync(Hash)).ShouldBeNull();
    }

    [Fact]
    public async Task GetTorrentTrackersAsync_RequestsHashAndParsesTrackers()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/trackers", Fixtures.Read("torrent-trackers.json"));

        IReadOnlyList<TorrentTracker>? trackers = await server.Client.GetTorrentTrackersAsync(Hash);

        server.SingleApiRequest.Method.ShouldBe(HttpMethod.Get);
        server.SingleApiRequest.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/trackers");
        server.SingleApiRequest.Uri.Query.ShouldBe($"?hash={Hash}");
        trackers.ShouldNotBeNull();
        trackers.Select(tracker => tracker.Url)
            .ShouldBe(["** [DHT] **", "http://tracker.example.com:6969/announce"]);
        trackers[1].TrackerStatus.ShouldBe(TorrentTrackerStatus.Working);
        trackers[1].Seeds.ShouldBe(12);
    }

    [Fact]
    public async Task GetTorrentTrackersAsync_NotFound_ReturnsNull()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/trackers", "", System.Net.HttpStatusCode.NotFound);

        (await server.Client.GetTorrentTrackersAsync(Hash)).ShouldBeNull();
    }
}
