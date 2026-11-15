using System.Net;
using System.Text.Json;
using Cleanuparr.Infrastructure.Transmission.Tests.TestHelpers;
using Shouldly;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Transmission.Tests;

public class TransmissionClientTorrentGetTests
{
    private static readonly string[] Fields =
    [
        TorrentFields.FILES, TorrentFields.FILE_STATS, TorrentFields.HASH_STRING, TorrentFields.ID, TorrentFields.ETA,
        TorrentFields.NAME, TorrentFields.STATUS, TorrentFields.IS_PRIVATE, TorrentFields.DOWNLOADED_EVER,
        TorrentFields.DOWNLOAD_DIR, TorrentFields.SECONDS_SEEDING, TorrentFields.TRACKER_STATS, TorrentFields.UPLOAD_RATIO,
        TorrentFields.TRACKERS, TorrentFields.RATE_DOWNLOAD, TorrentFields.TOTAL_SIZE, TorrentFields.LABELS,
        TorrentFields.IS_FINISHED, TorrentFields.ADDED_DATE, TorrentFields.ERROR, TorrentFields.ERROR_STRING,
    ];

    private static RpcTestClient ClientReturning(string body)
    {
        RpcTestClient rpc = new();
        rpc.Handler.SetupResponse(_ => RpcTestClient.Json(HttpStatusCode.OK, body));
        return rpc;
    }

    [Fact]
    public async Task TorrentGetAsync_RealPayload_DeserializesEveryRequestedField()
    {
        RpcTestClient rpc = ClientReturning(Fixtures.Read("torrent-get-4.0.6.json"));

        TransmissionTorrents? result = await rpc.Client.TorrentGetAsync(Fields);

        TorrentInfo torrent = result!.Torrents!.Single();
        torrent.Id.ShouldBe(7);
        torrent.HashString.ShouldBe("8c212779b4abde7c6bc608063a0d008b7e40ce32");
        torrent.Name.ShouldBe("show");
        torrent.Status.ShouldBe(6);
        torrent.Eta.ShouldBe(-1);
        torrent.DownloadDir.ShouldBe("/downloads/complete");
        torrent.DownloadedEver.ShouldBe(2048);
        torrent.SecondsSeeding.ShouldBe(3600);
        torrent.uploadRatio.ShouldBe(0.5);
        torrent.RateDownload.ShouldBe(512);
        torrent.TotalSize.ShouldBe(2048);
        torrent.AddedDate.ShouldBe(1700000000);
        torrent.Error.ShouldBe(0);
        torrent.ErrorString.ShouldBe("");
        torrent.Labels.ShouldBe(["tv", "keep"]);
        torrent.Files!.Length.ShouldBe(2);
        torrent.Files[1].Name.ShouldBe("show/ep1.exe");
        torrent.FileStats!.Length.ShouldBe(2);
        torrent.Trackers!.Single().Announce.ShouldBe("http://tracker.example/announce");
        torrent.TrackerStats!.Single().LastAnnounceSucceeded.ShouldBe(true);
        torrent.TrackerStats!.Single().SiteName.ShouldBe("tracker");
        result.Removed.ShouldBeEmpty();
    }

    [Fact]
    public async Task TorrentGetAsync_NumericBooleans_ConvertsZeroAndOne()
    {
        RpcTestClient rpc = ClientReturning(RpcTestClient.Envelope("success",
            """{"torrents":[{"id":1,"isPrivate":1,"isFinished":0,"wanted":[1,0,1],"fileStats":[{"wanted":0,"priority":1}]}]}"""));

        TransmissionTorrents? result = await rpc.Client.TorrentGetAsync(Fields);

        TorrentInfo torrent = result!.Torrents!.Single();
        torrent.IsPrivate.ShouldBe(true);
        torrent.IsFinished.ShouldBe(false);
        torrent.Wanted.ShouldBe([true, false, true]);
        torrent.FileStats!.Single().Wanted.ShouldBe(false);
    }

    [Fact]
    public async Task TorrentGetAsync_AvailabilityAsInteger_WrapsInArray()
    {
        RpcTestClient rpc = ClientReturning(RpcTestClient.Envelope("success", """{"torrents":[{"id":1,"availability":-1}]}"""));

        TransmissionTorrents? result = await rpc.Client.TorrentGetAsync(Fields);

        result!.Torrents!.Single().Availability.ShouldBe([-1]);
    }

    [Fact]
    public async Task TorrentGetAsync_AvailabilityAsArray_ReadsArray()
    {
        RpcTestClient rpc = ClientReturning(RpcTestClient.Envelope("success", """{"torrents":[{"id":1,"availability":[2,0,5]}]}"""));

        TransmissionTorrents? result = await rpc.Client.TorrentGetAsync(Fields);

        result!.Torrents!.Single().Availability.ShouldBe([2, 0, 5]);
    }

    [Fact]
    public async Task TorrentGetAsync_NoIds_OmitsIdsAndSendsFields()
    {
        RpcTestClient rpc = new("""{"torrents":[]}""");

        await rpc.Client.TorrentGetAsync(Fields);

        JsonElement body = JsonDocument.Parse(rpc.SingleRequest.Body!).RootElement;
        body.GetProperty("method").GetString().ShouldBe("torrent-get");
        JsonElement arguments = body.GetProperty("arguments");
        arguments.TryGetProperty("ids", out _).ShouldBeFalse();
        arguments.GetProperty("fields").GetArrayLength().ShouldBe(Fields.Length);
    }

    [Fact]
    public async Task TorrentGetAsync_HashOverload_SendsIdsArrayWithHash()
    {
        RpcTestClient rpc = new("""{"torrents":[]}""");

        await rpc.Client.TorrentGetAsync(Fields, "8c212779b4abde7c6bc608063a0d008b7e40ce32");

        JsonElement ids = JsonDocument.Parse(rpc.SingleRequest.Body!).RootElement.GetProperty("arguments").GetProperty("ids");
        ids.GetRawText().ShouldBe("[\"8c212779b4abde7c6bc608063a0d008b7e40ce32\"]");
    }
}
