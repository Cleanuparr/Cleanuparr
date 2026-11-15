using Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBittorrent.Client;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class ProxyTypeTests
{
    [Theory]
    [InlineData("2.8.0", ProxyType.HttpAuth, "3")]
    [InlineData("2.8.0", ProxyType.Socks5Auth, "4")]
    [InlineData("2.9.0", ProxyType.HttpAuth, "\"HTTP\"")]
    [InlineData("2.9.0", ProxyType.Socks5Auth, "\"SOCKS5\"")]
    [InlineData("2.9.0", ProxyType.Http, "\"HTTP\"")]
    [InlineData("2.9.0", ProxyType.Socks4, "\"SOCKS4\"")]
    [InlineData("2.9.0", ProxyType.None, "\"NONE\"")]
    public void Converter_MapsByApiVersion(string version, ProxyType type, string expected)
    {
        ProxyTypeConverter converter = new(ApiVersion.Parse(version));

        string json = JsonConvert.SerializeObject(new Preferences { ProxyType = type }, converter);

        json.ShouldBe($"{{\"proxy_type\":{expected}}}");
    }

    [Theory]
    [InlineData("2.11.4", ProxyType.HttpAuth, "HTTP", true)]
    [InlineData("2.11.4", ProxyType.Socks5Auth, "SOCKS5", true)]
    [InlineData("2.11.4", ProxyType.Http, "HTTP", false)]
    public async Task SetPreferencesAsync_NewApi_AuthTypeSetsAuthFlag(string version, ProxyType type, string expectedType, bool expectFlag)
    {
        using FakeQBittorrentServer server = new(version);

        await server.Client.SetPreferencesAsync(new Preferences { ProxyType = type });

        JObject posted = PostedJson(server);
        posted["proxy_type"]!.Value<string>().ShouldBe(expectedType);
        posted.ContainsKey("proxy_auth_enabled").ShouldBe(expectFlag);
    }

    [Fact]
    public async Task SetPreferencesAsync_OldApi_KeepsNumericTypeWithoutFlag()
    {
        using FakeQBittorrentServer server = new("2.8.0");

        await server.Client.SetPreferencesAsync(new Preferences { ProxyType = ProxyType.HttpAuth });

        server.SingleApiRequest.Body.ShouldNotBeNull();
        PostedJson(server).ToString(Formatting.None).ShouldBe("""{"proxy_type":3}""");
    }

    private static JObject PostedJson(FakeQBittorrentServer server)
    {
        string body = server.SingleApiRequest.Body!;
        return JObject.Parse(Uri.UnescapeDataString(body["json=".Length..].Replace('+', ' ')));
    }
}
