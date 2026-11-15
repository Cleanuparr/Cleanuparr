using Newtonsoft.Json;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class TorrentContentTests
{
    [Fact]
    public void IsSeeding_Present_Parses()
    {
        TorrentContent? content = JsonConvert.DeserializeObject<TorrentContent>("""{"index":0,"is_seed":true}""");

        content!.IsSeeding.ShouldBe(true);
    }

    [Fact]
    public void IsSeeding_Missing_IsNull()
    {
        TorrentContent? content = JsonConvert.DeserializeObject<TorrentContent>("""{"index":1,"name":"a"}""");

        content!.IsSeeding.ShouldBeNull();
    }
}
