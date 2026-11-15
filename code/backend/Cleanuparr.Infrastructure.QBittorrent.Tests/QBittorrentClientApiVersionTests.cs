using System.Net;
using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class QBittorrentClientApiVersionTests
{
    [Fact]
    public async Task GetApiVersionAsync_ParsesWebApiVersion()
    {
        using FakeQBittorrentServer server = new("2.11.4");

        ApiVersion version = await server.Client.GetApiVersionAsync();

        version.ShouldBe(new ApiVersion(2, 11, 4));
        server.Handler.Requests.Select(request => request.Uri.AbsolutePath)
            .ShouldBe(["/version/api", "/api/v2/app/webapiVersion"]);
    }

    [Fact]
    public async Task GetApiVersionAsync_LegacyEndpointServerError_FallsBackToV2()
    {
        using FakeQBittorrentServer server = new("2.9.3");
        server.Route("version/api", "", HttpStatusCode.InternalServerError);

        ApiVersion version = await server.Client.GetApiVersionAsync();

        version.ShouldBe(new ApiVersion(2, 9, 3));
    }

    [Fact]
    public async Task GetAlternativeSpeedLimitsEnabledAsync_ReturnsTrueForOne()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/transfer/speedLimitsMode", "1");

        bool enabled = await server.Client.GetAlternativeSpeedLimitsEnabledAsync();

        enabled.ShouldBeTrue();
        server.SingleApiRequest.Method.ShouldBe(HttpMethod.Get);
        server.SingleApiRequest.Uri.AbsolutePath.ShouldBe("/api/v2/transfer/speedLimitsMode");
    }

    [Fact]
    public async Task GetAlternativeSpeedLimitsEnabledAsync_ReturnsFalseForZero()
    {
        using FakeQBittorrentServer server = new();
        server.Route("api/v2/transfer/speedLimitsMode", "0");

        (await server.Client.GetAlternativeSpeedLimitsEnabledAsync()).ShouldBeFalse();
    }
}
