using System.Net;
using System.Text.Json;
using Cleanuparr.Infrastructure.Transmission.Tests.TestHelpers;
using Shouldly;
using Transmission.API.RPC;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Transmission.Tests;

public class TransmissionClientRequestTests
{
    private static HttpResponseMessage Conflict(string? newSessionId)
    {
        HttpResponseMessage response = new(HttpStatusCode.Conflict);
        if (newSessionId is not null)
        {
            response.Headers.Add("X-Transmission-Session-Id", newSessionId);
        }

        return response;
    }

    [Fact]
    public async Task CloseSessionAsync_ConflictWithSessionId_RetriesOnceWithNewHeader()
    {
        RpcTestClient rpc = new();
        int calls = 0;
        rpc.Handler.SetupResponse(_ => ++calls == 1
            ? Conflict("fresh")
            : RpcTestClient.Json(HttpStatusCode.OK, RpcTestClient.Envelope("success", "{}")));

        await rpc.Client.CloseSessionAsync();

        rpc.Handler.Requests.Count.ShouldBe(2);
        rpc.Handler.Requests[0].SessionId.ShouldBe("session");
        rpc.Handler.Requests[1].SessionId.ShouldBe("fresh");
        rpc.Client.SessionID.ShouldBe("fresh");
    }

    [Fact]
    public async Task CloseSessionAsync_SecondConflict_ThrowsSessionIdException()
    {
        RpcTestClient rpc = new();
        rpc.Handler.SetupResponse(_ => Conflict("fresh"));

        await Should.ThrowAsync<SessionIdException>(() => rpc.Client.CloseSessionAsync());

        rpc.Handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CloseSessionAsync_ConflictWithoutSessionIdHeader_ThrowsSessionIdException()
    {
        RpcTestClient rpc = new();
        rpc.Handler.SetupResponse(_ => Conflict(null));

        await Should.ThrowAsync<SessionIdException>(() => rpc.Client.CloseSessionAsync());

        rpc.Handler.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CloseSessionAsync_NonSuccessStatus_ThrowsHttpRequestExceptionWithStatusCode(HttpStatusCode status)
    {
        RpcTestClient rpc = new();
        rpc.Handler.SetupResponse(_ => new HttpResponseMessage(status));

        HttpRequestException exception = await Should.ThrowAsync<HttpRequestException>(() => rpc.Client.CloseSessionAsync());

        exception.StatusCode.ShouldBe(status);
    }

    [Fact]
    public async Task CloseSessionAsync_ResultNotSuccess_ThrowsRequestFailedExceptionWithMessage()
    {
        RpcTestClient rpc = new();
        rpc.Handler.SetupResponse(_ => RpcTestClient.Json(HttpStatusCode.OK, RpcTestClient.Envelope("no such torrent", "{}")));

        RequestFailedException exception = await Should.ThrowAsync<RequestFailedException>(() => rpc.Client.CloseSessionAsync());

        exception.Message.ShouldBe("no such torrent");
    }

    [Fact]
    public async Task CloseSessionAsync_NullJsonBody_ThrowsRequestFailedException()
    {
        RpcTestClient rpc = new();
        rpc.Handler.SetupResponse(_ => RpcTestClient.Json(HttpStatusCode.OK, "null"));

        RequestFailedException exception = await Should.ThrowAsync<RequestFailedException>(() => rpc.Client.CloseSessionAsync());

        exception.Message.ShouldBe("Empty response");
    }

    [Fact]
    public async Task CloseSessionAsync_WithCredentials_SendsBasicAuthorization()
    {
        RpcTestClient rpc = new(login: "user", password: "pass");

        await rpc.Client.CloseSessionAsync();

        rpc.SingleRequest.Authorization.ShouldBe("Basic dXNlcjpwYXNz");
    }

    [Fact]
    public async Task CloseSessionAsync_WithoutCredentials_SendsNoAuthorization()
    {
        RpcTestClient rpc = new();

        await rpc.Client.CloseSessionAsync();

        rpc.SingleRequest.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task CloseSessionAsync_RepeatedCalls_IncrementsTag()
    {
        RpcTestClient rpc = new();

        await rpc.Client.CloseSessionAsync();
        await rpc.Client.CloseSessionAsync();

        rpc.Client.CurrentTag.ShouldBe(2);
        TagOf(rpc.Handler.Requests[0]).ShouldBe(1);
        TagOf(rpc.Handler.Requests[1]).ShouldBe(2);
    }

    [Fact]
    public async Task TorrentSetLocationAsync_NonAsciiAndAmpersand_RoundTripsLocation()
    {
        RpcTestClient rpc = new();
        const string location = "/downloads/Tom & Jerry/日本語 é";

        await rpc.Client.TorrentSetLocationAsync([7], location, true);

        JsonElement arguments = Body(rpc.SingleRequest).GetProperty("arguments");
        arguments.GetProperty("location").GetString().ShouldBe(location);
    }

    [Fact]
    public async Task TorrentSetLocationAsync_AnyLocation_SendsMethodAndArguments()
    {
        RpcTestClient rpc = new();

        await rpc.Client.TorrentSetLocationAsync([7, 8], "/data", false);

        JsonElement body = Body(rpc.SingleRequest);
        body.GetProperty("method").GetString().ShouldBe("torrent-set-location");
        JsonElement arguments = body.GetProperty("arguments");
        arguments.GetProperty("ids").GetRawText().ShouldBe("[7,8]");
        arguments.GetProperty("move").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task TorrentRemoveAsync_DeleteData_SendsMethodAndArguments()
    {
        RpcTestClient rpc = new();

        await rpc.Client.TorrentRemoveAsync([7], true);

        JsonElement body = Body(rpc.SingleRequest);
        body.GetProperty("method").GetString().ShouldBe("torrent-remove");
        body.GetProperty("arguments").GetProperty("ids").GetRawText().ShouldBe("[7]");
        body.GetProperty("arguments").GetProperty("delete-local-data").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task TorrentStopAsync_HashId_SendsMethodAndIds()
    {
        RpcTestClient rpc = new();

        await rpc.Client.TorrentStopAsync(["abc"]);

        JsonElement body = Body(rpc.SingleRequest);
        body.GetProperty("method").GetString().ShouldBe("torrent-stop");
        body.GetProperty("arguments").GetProperty("ids").GetRawText().ShouldBe("[\"abc\"]");
    }

    [Fact]
    public async Task TorrentStartAsync_NumericId_SendsMethodAndIds()
    {
        RpcTestClient rpc = new();

        await rpc.Client.TorrentStartAsync([7L]);

        JsonElement body = Body(rpc.SingleRequest);
        body.GetProperty("method").GetString().ShouldBe("torrent-start");
        body.GetProperty("arguments").GetProperty("ids").GetRawText().ShouldBe("[7]");
    }

    [Fact]
    public async Task BandwidthGroupGetAsync_WithGroups_SendsNameKey()
    {
        RpcTestClient rpc = new("""{"group":[{"name":"slow","honorsSessionLimits":true,"speed-limit-down":10,"speed-limit-down-enabled":true,"speed-limit-up":5,"speed-limit-up-enabled":false}]}""");

        BandwidthGroup[]? groups = await rpc.Client.BandwidthGroupGetAsync(["slow"]);

        JsonElement body = Body(rpc.SingleRequest);
        body.GetProperty("method").GetString().ShouldBe("group-get");
        body.GetProperty("arguments").GetProperty("name").GetRawText().ShouldBe("[\"slow\"]");
        groups.ShouldNotBeNull();
        groups.Single().Name.ShouldBe("slow");
        groups.Single().SpeedLimitDown.ShouldBe(10);
    }

    private static JsonElement Body(RecordedRequest request) => JsonDocument.Parse(request.Body!).RootElement;

    private static long TagOf(RecordedRequest request) => Body(request).GetProperty("tag").GetInt64();
}
