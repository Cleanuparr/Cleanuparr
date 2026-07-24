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

/// <summary>
/// Characterization tests for the Deluge JSON-RPC request/response handling, driven through the real
/// <see cref="DelugeClient"/>. Written against the current Newtonsoft behavior; must stay green after the
/// System.Text.Json migration.
/// </summary>
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
}
