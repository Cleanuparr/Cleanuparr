using Newtonsoft.Json;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class StringToListConverterTests
{
    [Fact]
    public void Serialize_NonListCollection_JoinsWithSeparator()
    {
        TorrentInfo info = new() { Tags = new HashSet<string> { "a", "b" } };

        string json = JsonConvert.SerializeObject(info);

        json.ShouldContain("\"tags\":\"a,b\"");
    }

    [Fact]
    public void Deserialize_PeerFiles_StaysListAndSplitsLines()
    {
        PeerPartialInfo peer = JsonConvert.DeserializeObject<PeerPartialInfo>("""{"files":"a\nb"}""")!;

        peer.Files.ShouldBe(["a", "b"]);
    }
}
