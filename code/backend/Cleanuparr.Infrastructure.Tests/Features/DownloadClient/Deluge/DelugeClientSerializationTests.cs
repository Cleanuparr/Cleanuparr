using System.Net;
using System.Text.Json.Nodes;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.Deluge;

public class DelugeClientSerializationTests
{
    private static (DelugeClient client, FakeHttpMessageHandler handler) CreateClient(string responseJson)
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        }));

        DownloadClientConfig config = new()
        {
            Name = "deluge",
            TypeName = DownloadClientTypeName.Deluge,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8112")
        };

        return (new DelugeClient(config, new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task Request_SerializesJsonRpcEnvelope_WithIdMethodAndParams()
    {
        (DelugeClient client, FakeHttpMessageHandler handler) =
            CreateClient("""{"id":1,"result":{},"error":null}""");

        await client.GetStatusForAllTorrents();

        JsonObject body = JsonNode.Parse(handler.CapturedRequestBodies[0]!)!.AsObject();
        body["id"]!.GetValue<int>().ShouldBe(1);
        body["method"]!.GetValue<string>().ShouldBe("core.get_torrents_status");

        JsonArray parameters = body["params"]!.AsArray();
        parameters.Count.ShouldBe(2);
        parameters[0]!.GetValue<string>().ShouldBe("");
        parameters[1]!.AsArray().Select(n => n!.GetValue<string>())
            .ShouldContain("download_payload_rate");
    }

    [Fact]
    public async Task Response_WithError_ThrowsDelugeClientException()
    {
        (DelugeClient client, _) =
            CreateClient("""{"id":1,"result":null,"error":{"message":"boom","code":3}}""");

        DelugeClientException ex = await Should.ThrowAsync<DelugeClientException>(
            () => client.GetStatusForAllTorrents());
        ex.Message.ShouldBe("boom");
    }

    [Fact]
    public async Task Response_WithMismatchedId_ThrowsDesync()
    {
        (DelugeClient client, _) =
            CreateClient("""{"id":999,"result":{},"error":null}""");

        DelugeClientException ex = await Should.ThrowAsync<DelugeClientException>(
            () => client.GetStatusForAllTorrents());
        ex.Message.ShouldBe("desync");
    }

    [Fact]
    public async Task Response_WithUnknownExtraFields_StillDeserializes()
    {
        const string response = """
            {
                "id": 1,
                "result": {
                    "abc": {
                        "hash": "abc",
                        "state": "Seeding",
                        "name": "T",
                        "total_size": 10,
                        "total_done": 10,
                        "is_finished": true,
                        "download_payload_rate": 0,
                        "seeding_time": 0,
                        "ratio": 1.0,
                        "trackers": [],
                        "download_location": "/d",
                        "some_future_field": "ignored"
                    }
                },
                "error": null,
                "unexpected_top_level": 42
            }
            """;
        (DelugeClient client, _) = CreateClient(response);

        List<DownloadStatus>? statuses = await client.GetStatusForAllTorrents();

        statuses.ShouldNotBeNull();
        DownloadStatus status = statuses.ShouldHaveSingleItem();
        status.Hash.ShouldBe("abc");
        status.State.ShouldBe(DelugeState.Seeding);
        status.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTorrentStatus_WithDisconnectedDaemonError_ReturnsNull()
    {
        (DelugeClient client, _) = CreateClient(
            """{"id":1,"result":null,"error":{"message":"AttributeError: 'NoneType' object has no attribute 'call'","code":4}}""");

        DownloadStatus? status = await client.GetTorrentStatus("abc");

        status.ShouldBeNull();
    }

    [Fact]
    public async Task GetTorrentStatus_WithUnknownHashError_ReturnsNull()
    {
        const string response = """
            {
                "id": 1,
                "result": null,
                "error": {
                    "message": "Failure: [Failure instance: Traceback (failure with no frames): <class 'deluge.error.WrappedException'>: [Errno 32] Broken pipe\nTraceback (most recent call last):\n  File \"/opt/deluge-2.1.1/lib/python3.9/site-packages/deluge/core/torrentmanager.py\", line 308, in __getitem__\n    return self.torrents[torrent_id]\nKeyError: '5a64675bf2d466929fc6a916e3a975fa6940975b'\n]",
                    "code": 4
                }
            }
            """;
        (DelugeClient client, _) = CreateClient(response);

        DownloadStatus? status = await client.GetTorrentStatus("5a64675bf2d466929fc6a916e3a975fa6940975b");

        status.ShouldBeNull();
    }

    [Fact]
    public async Task GetTorrentStatus_WithUnrelatedError_Throws()
    {
        (DelugeClient client, _) =
            CreateClient("""{"id":1,"result":null,"error":{"message":"Not authenticated","code":1}}""");

        DelugeClientException ex = await Should.ThrowAsync<DelugeClientException>(
            () => client.GetTorrentStatus("5a64675bf2d466929fc6a916e3a975fa6940975b"));
        ex.Message.ShouldBe("Not authenticated");
    }

    [Fact]
    public async Task GetTorrentFiles_WithNestedDirectory_DeserializesWithoutIndex()
    {
        const string response = """
            {
                "id": 1,
                "result": {
                    "type": "dir",
                    "contents": {
                        "Some.Release": {
                            "type": "dir",
                            "priority": 1,
                            "progress": 1.0,
                            "progresses": [1.0],
                            "size": 200,
                            "path": "Some.Release",
                            "contents": {
                                "video.mkv": {
                                    "type": "file",
                                    "index": 0,
                                    "offset": 0,
                                    "path": "Some.Release/video.mkv",
                                    "priority": 1,
                                    "progress": 1.0,
                                    "size": 200
                                }
                            }
                        }
                    }
                },
                "error": null
            }
            """;
        (DelugeClient client, _) = CreateClient(response);

        DelugeContents? contents = await client.GetTorrentFiles("abc");

        contents.ShouldNotBeNull();
        DelugeFileOrDirectory directory = contents.Contents!["Some.Release"];
        directory.Type.ShouldBe("dir");
        directory.Contents!["video.mkv"].Index.ShouldBe(0);
    }

    // Each response below comes from a Deluge 2.2.0 daemon.

    [Fact]
    public async Task DeleteTorrents_WithEmptyFailureList_DoesNotThrow()
    {
        (DelugeClient client, _) = CreateClient("""{"result": [], "error": null, "id": 1}""");

        await client.DeleteTorrents(["5a64675bf2d466929fc6a916e3a975fa6940975b"], true);
    }

    [Fact]
    public async Task DeleteTorrents_WithFailedHashes_DoesNotThrow()
    {
        const string response = """
            {"result": [["0000000000000000000000000000000000000000", "torrent_id 0000000000000000000000000000000000000000 not in session."]], "error": null, "id": 1}
            """;
        (DelugeClient client, _) = CreateClient(response);

        await client.DeleteTorrents(["0000000000000000000000000000000000000000"], true);
    }

    [Fact]
    public async Task ChangeFilesPriority_WithNullResult_DoesNotThrow()
    {
        (DelugeClient client, _) = CreateClient("""{"result": null, "error": null, "id": 1}""");

        await client.ChangeFilesPriority("abc", [1, 0]);
    }

    [Fact]
    public async Task GetHost_WithNumericPort_ReturnsHostId()
    {
        const string response = """
            {"result": [["a0de4463cfe441f2be873bde7cdf2d22", "127.0.0.1", 58846, "localclient"]], "error": null, "id": 1}
            """;
        (DelugeClient client, _) = CreateClient(response);

        string? hostId = await client.GetHost();

        hostId.ShouldBe("a0de4463cfe441f2be873bde7cdf2d22");
    }

    [Fact]
    public async Task GetHost_WithNoHosts_ReturnsNull()
    {
        (DelugeClient client, _) = CreateClient("""{"result": [], "error": null, "id": 1}""");

        string? hostId = await client.GetHost();

        hostId.ShouldBeNull();
    }

    [Fact]
    public async Task GetTorrentFiles_WithFileLargerThan2Gib_Deserializes()
    {
        const string response = """
            {
                "result": {
                    "contents": {
                        "big.mkv": {"type": "file", "index": 0, "path": "big.mkv", "size": 3221225472, "offset": 0, "progress": 0.0, "priority": 4},
                        "second.mkv": {"type": "file", "index": 1, "path": "second.mkv", "size": 1024, "offset": 3221225472, "progress": 0.0, "priority": 4}
                    },
                    "type": "dir"
                },
                "error": null,
                "id": 1
            }
            """;
        (DelugeClient client, _) = CreateClient(response);

        DelugeContents? contents = await client.GetTorrentFiles("abc");

        contents.ShouldNotBeNull();
        contents.Contents!["big.mkv"].Size.ShouldBe(3221225472);
        contents.Contents["second.mkv"].Offset.ShouldBe(3221225472);
    }

    [Fact]
    public async Task GetTorrentFiles_WithA5GbFileAtTheRoot_Deserializes()
    {
        // The response of issue #706, with the size that Deluge sent.
        const string response = """
            {"result": {"contents": {"Drive (2011) GBR MULTi VFF 2160p 10bit 4KLight DOLBY VISION BluRay DDP 7.1 x265-QTZ.mkv": {"type": "file", "index": 0, "path": "Drive (2011) GBR MULTi VFF 2160p 10bit 4KLight DOLBY VISION BluRay DDP 7.1 x265-QTZ.mkv", "size": 5010493964, "offset": 0, "progress": 1.0, "priority": 1}}, "type": "dir"}, "error": null, "id": 1}
            """;
        (DelugeClient client, _) = CreateClient(response);

        DelugeContents? contents = await client.GetTorrentFiles("abc");

        contents.ShouldNotBeNull();
        DelugeFileOrDirectory file = contents.Contents!.Values.ShouldHaveSingleItem();
        file.Size.ShouldBe(5010493964);
        file.Priority.ShouldBe(1);
    }

    [Fact]
    public async Task GetTorrentFiles_WithA6GbFileInADirectory_Deserializes()
    {
        // The response of issue #707. The directory node also holds a large size.
        const string response = """
            {"result": {"contents": {"a-release": {"type": "dir", "contents": {"a-release.mkv": {"type": "file", "index": 2, "path": "a-release/a-release.mkv", "size": 6116913821, "offset": 497025, "progress": 0.9986286163330078, "priority": 4}}, "size": 6117415095, "priority": 4, "progress": 0.9986287287071014}}, "type": "dir"}, "error": null, "id": 1}
            """;
        (DelugeClient client, _) = CreateClient(response);

        DelugeContents? contents = await client.GetTorrentFiles("abc");

        contents.ShouldNotBeNull();
        DelugeFileOrDirectory directory = contents.Contents!["a-release"];
        directory.Size.ShouldBe(6117415095);
        DelugeFileOrDirectory file = directory.Contents!["a-release.mkv"];
        file.Size.ShouldBe(6116913821);
        file.Offset.ShouldBe(497025);
        file.Index.ShouldBe(2);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-3600)]
    public async Task GetStatusForAllTorrents_WithNegativeEta_Deserializes(long eta)
    {
        // Deluge gives -1 if the download needs more than one year.
        string response = $$"""
            {
                "result": {
                    "abc": {
                        "hash": "abc",
                        "state": "Downloading",
                        "name": "T",
                        "eta": {{eta}},
                        "private": true,
                        "total_done": 0,
                        "is_finished": false,
                        "seeding_time": 0,
                        "ratio": -1.0,
                        "total_seeds": -1,
                        "download_payload_rate": 1,
                        "total_size": 3221225472,
                        "download_location": "/downloads",
                        "trackers": []
                    }
                },
                "error": null,
                "id": 1
            }
            """;
        (DelugeClient client, _) = CreateClient(response);

        List<DownloadStatus>? statuses = await client.GetStatusForAllTorrents();

        DownloadStatus status = statuses.ShouldNotBeNull().ShouldHaveSingleItem();
        status.Eta.ShouldBe(eta);
        status.Size.ShouldBe(3221225472);
    }

    [Fact]
    public void ItemWrapper_WithNegativeEta_ReportsZero()
    {
        DelugeItemWrapper wrapper = new(new DownloadStatus { Hash = "abc", Eta = -1 });

        wrapper.Eta.ShouldBe(0);
    }

    [Fact]
    public async Task Response_WithErrorAndUnexpectedResultShape_KeepsTheErrorMessage()
    {
        (DelugeClient client, _) =
            CreateClient("""{"id":1,"result":[],"error":{"message":"boom","code":3}}""");

        DelugeClientException ex = await Should.ThrowAsync<DelugeClientException>(
            () => client.GetStatusForAllTorrents());
        ex.Message.ShouldBe("boom");
    }

    [Fact]
    public async Task Response_WithUnexpectedResultShape_ThrowsWithTheMethodAndTheBody()
    {
        (DelugeClient client, _) = CreateClient("""{"id":1,"result":"not-a-map","error":null}""");

        DelugeClientException ex = await Should.ThrowAsync<DelugeClientException>(
            () => client.GetStatusForAllTorrents());
        ex.Message.ShouldContain("core.get_torrents_status");
        ex.Message.ShouldContain("not-a-map");
    }
}
