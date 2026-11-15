using Newtonsoft.Json;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class NullableTorrentStateConverterTests
{
    [Fact]
    public void PartialInfo_NullState_DeserializesToNull()
    {
        TorrentPartialInfo info = JsonConvert.DeserializeObject<TorrentPartialInfo>("""{"state":null}""")!;

        info.State.ShouldBeNull();
    }

    [Fact]
    public void PartialInfo_StoppedState_Parses()
    {
        TorrentPartialInfo info = JsonConvert.DeserializeObject<TorrentPartialInfo>("""{"state":"stoppedUP"}""")!;

        info.State.ShouldBe(TorrentState.PausedUpload);
    }

    [Fact]
    public void PartialInfo_NullState_SerializesAsNull()
    {
        string json = JsonConvert.SerializeObject(new TorrentPartialInfo());

        json.ShouldContain("\"state\":null");
    }

    [Fact]
    public void TorrentInfo_NullState_StillThrows()
    {
        Should.Throw<JsonSerializationException>(() => JsonConvert.DeserializeObject<TorrentInfo>("""{"state":null}"""));
    }
}
