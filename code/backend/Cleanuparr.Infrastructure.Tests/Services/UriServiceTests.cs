using Cleanuparr.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class UriServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDomain_NullOrWhitespace_ReturnsNull(string? input)
    {
        UriService.GetDomain(input).ShouldBeNull();
    }

    [Theory]
    [InlineData("http://tracker.example.com/announce", "tracker.example.com")]
    [InlineData("http://tracker.example.com:8080/announce", "tracker.example.com")]
    [InlineData("https://tracker.example.com/announce", "tracker.example.com")]
    [InlineData("https://tracker.example.com:443/announce", "tracker.example.com")]
    public void GetDomain_HttpUrls_ReturnsDomain(string input, string expected)
    {
        UriService.GetDomain(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("udp://tracker.opentrackr.org:1337/announce", "tracker.opentrackr.org")]
    [InlineData("udp://open.stealth.si:80/announce", "open.stealth.si")]
    public void GetDomain_UdpTrackerUrls_ReturnsDomain(string input, string expected)
    {
        UriService.GetDomain(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("tracker.example.com", "tracker.example.com")]
    [InlineData("tracker.example.com:8080", "tracker.example.com")]
    [InlineData("tracker.example.com/announce", "tracker.example.com")]
    public void GetDomain_NoScheme_PrependsHttpAndReturnsDomain(string input, string expected)
    {
        UriService.GetDomain(input).ShouldBe(expected);
    }

    [Fact]
    public void GetDomain_PlainDomain_ReturnsDomain()
    {
        UriService.GetDomain("example.com").ShouldBe("example.com");
    }
}
