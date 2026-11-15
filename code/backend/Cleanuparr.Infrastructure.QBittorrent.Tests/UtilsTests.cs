using System.Net;
using Shouldly;
using U = QBittorrent.Client.Internal.Utils;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class UtilsTests
{
    [Fact]
    public void ParseValidIp4Endpoint()
    {
        IPEndPoint endpoint = U.ParseIpEndpoint("127.0.0.1:8080");
        endpoint.Address.ShouldBe(IPAddress.Loopback);
        endpoint.Port.ShouldBe(8080);
    }

    [Fact]
    public void ParseValidIp6Endpoint()
    {
        IPEndPoint endpoint = U.ParseIpEndpoint("[::1]:8080");
        endpoint.Address.ShouldBe(IPAddress.IPv6Loopback);
        endpoint.Port.ShouldBe(8080);
    }

    [Theory]
    [InlineData("127.0.0.1:0")]
    [InlineData("[::1]:0")]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    [InlineData(":8080")]
    [InlineData("[]:8080")]
    [InlineData("::1:8080")]
    public void ParseInvalidEndpointThrows(string value)
    {
        Should.Throw<FormatException>(() => U.ParseIpEndpoint(value));
    }
}
