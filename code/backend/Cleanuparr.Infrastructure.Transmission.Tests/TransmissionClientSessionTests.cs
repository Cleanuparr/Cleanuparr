using System.Net;
using System.Text;
using Cleanuparr.Infrastructure.Transmission.Tests.TestHelpers;
using Shouldly;
using Transmission.API.RPC;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Transmission.Tests;

public class TransmissionClientSessionTests
{
    [Fact]
    public async Task GetSessionInformationAsync_Transmission406Payload_DeserializesNumericFreeSpace()
    {
        FakeHttpMessageHandler handler = new();
        handler.SetupResponse(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Fixtures.Read("session-get-4.0.6.json"), Encoding.UTF8, "application/json")
        });
        Client client = new(new HttpClient(handler), "http://localhost:9091/transmission/rpc", sessionID: "session");

        SessionInfo? session = await client.GetSessionInformationAsync();

        session.ShouldNotBeNull();
#pragma warning disable CS0618
        session.DownloadDirectoryFreeSpace.ShouldBe(-1);
#pragma warning restore CS0618
        session.AlternativeSpeedEnabled.ShouldBe(false);
        session.SeedRatioLimit.ShouldBe(2);
        session.Units.ShouldNotBeNull();
    }
}
