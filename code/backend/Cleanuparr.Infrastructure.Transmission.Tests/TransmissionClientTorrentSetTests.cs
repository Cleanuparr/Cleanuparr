using System.Text.Json;
using Cleanuparr.Infrastructure.Transmission.Tests.TestHelpers;
using Shouldly;
using Transmission.API.RPC.Arguments;

namespace Cleanuparr.Infrastructure.Transmission.Tests;

public class TransmissionClientTorrentSetTests
{
    private static async Task<JsonElement> SendAsync(TorrentSettings settings)
    {
        RpcTestClient rpc = new();

        await rpc.Client.TorrentSetAsync(settings);

        JsonElement body = JsonDocument.Parse(rpc.SingleRequest.Body!).RootElement;
        body.GetProperty("method").GetString().ShouldBe("torrent-set");
        return body.GetProperty("arguments");
    }

    [Fact]
    public async Task TorrentSetAsync_IdsFilesUnwantedAndLabels_SerializesArrays()
    {
        JsonElement arguments = await SendAsync(new TorrentSettings
        {
            Ids = ["abc"],
            FilesUnwanted = [0, 2],
            Labels = ["tv", "keep"],
        });

        arguments.GetProperty("ids").GetRawText().ShouldBe("[\"abc\"]");
        arguments.GetProperty("files-unwanted").GetRawText().ShouldBe("[0,2]");
        arguments.GetProperty("labels").GetRawText().ShouldBe("[\"tv\",\"keep\"]");
    }

    [Fact]
    public async Task TorrentSetAsync_TrackerReplace_SerializesFlattenedIdUrlPairs()
    {
#pragma warning disable CS0618
        JsonElement arguments = await SendAsync(new TorrentSettings
        {
            TrackerReplace = [new KeyValuePair<int, string>(0, "http://a/announce"), new KeyValuePair<int, string>(1, "http://b/announce")],
        });
#pragma warning restore CS0618

        JsonElement replace = arguments.GetProperty("trackerReplace");
        replace.GetArrayLength().ShouldBe(4);
        replace[0].GetInt32().ShouldBe(0);
        replace[1].GetString().ShouldBe("http://a/announce");
        replace[2].GetInt32().ShouldBe(1);
        replace[3].GetString().ShouldBe("http://b/announce");
    }

    [Fact]
    public void TrackerReplace_RoundTrip_ReturnsOriginalPairs()
    {
        TorrentSettings settings = new();
#pragma warning disable CS0618
        settings.TrackerReplace = [new KeyValuePair<int, string>(3, "http://a/announce")];

        settings.TrackerReplace!.Single().ShouldBe(new KeyValuePair<int, string>(3, "http://a/announce"));
#pragma warning restore CS0618
    }

    [Fact]
    public async Task TorrentSetAsync_TrackerList_SerializesAsString()
    {
        JsonElement arguments = await SendAsync(new TorrentSettings { TrackerList = "http://a/announce\n\nhttp://b/announce" });

        JsonElement list = arguments.GetProperty("trackerList");
        list.ValueKind.ShouldBe(JsonValueKind.String);
        list.GetString().ShouldBe("http://a/announce\n\nhttp://b/announce");
    }

    [Fact]
    public async Task TorrentSetAsync_FilePriorities_SerializeAsNumericArrays()
    {
        JsonElement arguments = await SendAsync(new TorrentSettings
        {
            PriorityHigh = [0],
            PriorityNormal = [1, 2],
            PriorityLow = [3],
        });

        arguments.GetProperty("priority-high").GetRawText().ShouldBe("[0]");
        arguments.GetProperty("priority-normal").GetRawText().ShouldBe("[1,2]");
        arguments.GetProperty("priority-low").GetRawText().ShouldBe("[3]");
    }
}
