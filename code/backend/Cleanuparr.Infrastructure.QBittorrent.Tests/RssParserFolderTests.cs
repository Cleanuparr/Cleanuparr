using Newtonsoft.Json.Linq;
using QBittorrent.Client;
using QBittorrent.Client.Internal;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class RssParserFolderTests
{
    [Fact]
    public void Parse_FolderContainingItemNamedUid_ParsesAsFolder()
    {
        JObject root = JObject.Parse("""
            {
              "uid": { "uid": "80858e34-897e-48bb-bfe3-ed0fec030861", "url": "http://example.com/a.rss" },
              "sub": { "uid": { "uid": "11111111-897e-48bb-bfe3-ed0fec030861", "url": "http://example.com/b.rss" } }
            }
            """);

        RssFolder rss = RssParser.Parse(root);

        rss.Items.Count.ShouldBe(2);
        rss.Items.OfType<RssFeed>().Single().Name.ShouldBe("uid");
        RssFolder sub = rss.Items.OfType<RssFolder>().Single();
        sub.Name.ShouldBe("sub");
        sub.Items.Single().ShouldBeOfType<RssFeed>().Name.ShouldBe("uid");
    }
}
