using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientTorrentListTests
{
    private const string FirstHash = "8c212779b4abde7c6bc608063a0d008b7e40ce32";

    [Fact]
    public async Task GetTorrentListAsync_EmptyQuery_RequestsPlainList()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", "[]");

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        result.ShouldBeEmpty();
        server.SingleApiRequest.Method.ShouldBe(HttpMethod.Get);
        server.SingleApiRequest.Uri.AbsolutePath.ShouldBe("/api/v2/torrents/info");
        server.SingleApiRequest.Uri.Query.ShouldBe("?filter=all&reverse=false");
    }

    [Fact]
    public async Task GetTorrentListAsync_NullQuery_RequestsPlainList()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", "[]");

        await server.Client.GetTorrentListAsync(null);

        server.SingleApiRequest.Uri.Query.ShouldBe("?filter=all&reverse=false");
    }

    [Fact]
    public async Task GetTorrentListAsync_HashesQuery_JoinsHashesWithPipe()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", "[]");

        await server.Client.GetTorrentListAsync(new TorrentListQuery { Hashes = [FirstHash, "E4BE9E4DB876E3E3179778B03E906297BE5C8DBE"] });

        server.SingleApiRequest.Uri.Query.ShouldBe($"?filter=all&reverse=false&hashes={FirstHash}%7CE4BE9E4DB876E3E3179778B03E906297BE5C8DBE");
    }

    [Fact]
    public async Task GetTorrentListAsync_CompletedFilter_SendsFilterParameter()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", "[]");

        await server.Client.GetTorrentListAsync(new TorrentListQuery { Filter = TorrentListFilter.Completed });

        server.SingleApiRequest.Uri.Query.ShouldBe("?filter=completed&reverse=false");
    }

    [Fact]
    public async Task GetTorrentListAsync_ParsesRealisticResponse()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", Fixtures.Read("torrents-info.json"));

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        result.Count.ShouldBe(3);
        TorrentInfo first = result[0];
        first.Hash.ShouldBe(FirstHash);
        first.Name.ShouldBe("ubuntu-24.04-desktop-amd64.iso");
        first.State.ShouldBe(TorrentState.PausedUpload);
        first.Category.ShouldBe("linux");
        first.Tags.ShouldBe(["a", "b"]);
        first.Size.ShouldBe(4294967296);
        first.Progress.ShouldBe(1);
        first.Ratio.ShouldBe(1.25);
        first.SavePath.ShouldBe("/downloads");
        first.ContentPath.ShouldBe("/downloads/ubuntu-24.04-desktop-amd64.iso");
        first.CurrentTracker.ShouldBe("http://tracker.example.com:6969/announce");
        first.AddedOn.ShouldBe(new DateTime(2023, 11, 14, 22, 13, 20));
        first.CompletionOn.ShouldBe(new DateTime(2023, 11, 14, 23, 3, 20));
        first.SeedingTime.ShouldBe(TimeSpan.FromHours(1));
        first.DownloadLimit.ShouldBeNull();
        first.UploadLimit.ShouldBeNull();
    }

    [Theory]
    [InlineData("stoppedUP", TorrentState.PausedUpload)]
    [InlineData("stoppedDL", TorrentState.PausedDownload)]
    [InlineData("pausedUP", TorrentState.PausedUpload)]
    [InlineData("pausedDL", TorrentState.PausedDownload)]
    public async Task GetTorrentListAsync_MapsPausedAndStoppedStates(string state, TorrentState expected)
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", $$"""[{"hash":"{{FirstHash}}","state":"{{state}}"}]""");

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        result.Single().State.ShouldBe(expected);
    }

    [Fact]
    public async Task GetTorrentListAsync_FixtureStatesParseAsExpected()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", Fixtures.Read("torrents-info.json"));

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        result.Select(torrent => torrent.State)
            .ShouldBe([TorrentState.PausedUpload, TorrentState.PausedDownload, TorrentState.PausedUpload]);
    }

    [Theory]
    [InlineData("a, b", new[] { "a", "b" })]
    [InlineData("a,b", new[] { "a", "b" })]
    [InlineData("solo", new[] { "solo" })]
    [InlineData("", new string[0])]
    public async Task GetTorrentListAsync_ParsesTags(string tags, string[] expected)
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", $$"""[{"hash":"{{FirstHash}}","state":"uploading","tags":"{{tags}}"}]""");

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        result.Single().Tags.ShouldBe(expected);
    }

    [Fact]
    public async Task GetTorrentListAsync_EmptyTagsFromFixture_ParseAsEmptyList()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", Fixtures.Read("torrents-info.json"));

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        result[1].Tags.ShouldBeEmpty();
        result[1].Category.ShouldBe("");
    }

    [Fact]
    public async Task GetTorrentListAsync_MissingTags_LeavesTagsNull()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", $$"""[{"hash":"{{FirstHash}}","state":"uploading"}]""");

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        result.Single().Tags.ShouldBeNull();
    }

    [Fact]
    public async Task GetTorrentListAsync_NegativeCompletionOn_ParsesAsNull()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", Fixtures.Read("torrents-info.json"));

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        result[1].CompletionOn.ShouldBeNull();
        result[1].AddedOn.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("-1", null)]
    [InlineData("0", null)]
    [InlineData("1", "1970-01-01T00:00:01")]
    [InlineData("1700000000", "2023-11-14T22:13:20")]
    [InlineData("253402300799", "9999-12-31T23:59:59")]
    [InlineData("253402300800", null)]
    [InlineData("9223372036854775807", null)]
    public async Task GetTorrentListAsync_ParsesTimestampEdges(string timestamp, string? expected)
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info",
            $$"""[{"hash":"{{FirstHash}}","state":"uploading","added_on":{{timestamp}},"completion_on":{{timestamp}}}]""");

        IReadOnlyList<TorrentInfo> result = await server.Client.GetTorrentListAsync(new TorrentListQuery());

        DateTime? expectedValue = expected is null ? null : DateTime.Parse(expected);
        result.Single().AddedOn.ShouldBe(expectedValue);
        result.Single().CompletionOn.ShouldBe(expectedValue);
    }

    [Fact]
    public async Task GetTorrentListAsync_JsonNullBody_ReturnsNull()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/torrents/info", "null");

        (await server.Client.GetTorrentListAsync(new TorrentListQuery())).ShouldBeNull();
    }
}
