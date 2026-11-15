using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Shouldly;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBittorrent.Client;
using QBittorrent.Client.Internal;
using Xunit;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests
{
    public class RssParserTests
    {
        [Fact]
        public void ParseValid()
        {
            JObject root = ReadJsonFromResource("Cleanuparr.Infrastructure.QBittorrent.Tests.data.rss-valid.json");
            RssFolder rss = RssParser.Parse(root);

            RssFolder expected = new RssFolder(
                new RssItem[]
                {
                    new RssFeed
                    {
                        Name = "Ubuntu",
                        Title = "Ubuntu downloads",
                        Uid = Guid.Parse("{80858e34-897e-48bb-bfe3-ed0fec030861}"),
                        Url = new Uri("http://example.com/ubuntu.rss"),
                        LastBuildDate = new DateTimeOffset(2018, 11, 29, 9, 34, 01, TimeSpan.FromHours(-5)),
                        IsLoading = false,
                        HasError = false,
                        Articles = new List<RssArticle>
                        {
                            new RssArticle
                            {
                                Date = new DateTimeOffset(2018, 11, 29, 10, 56, 14, TimeSpan.Zero),
                                Id = "http://releases.ubuntu.com/16.04/ubuntu-16.04.4-desktop-amd64.iso.torrent",
                                Link = new Uri("https://www.ubuntu.com/download/desktop"),
                                Title = "Ubuntu 16.04.4",
                                TorrentUri = new Uri("http://releases.ubuntu.com/16.04/ubuntu-16.04.4-desktop-amd64.iso.torrent"),
                                IsRead = false,
                                AdditionalData = new Dictionary<string, JToken>
                                {
                                    ["fileName"] = "ubuntu-16.04.4-desktop-amd64.iso",
                                    ["infoHash"] = "778CE280B595E57780FF083F2EB6F897DFA4A4EE",
                                    ["magnetURI"] = "magnet:?xt=urn:btih:778ce280b595e57780ff083f2eb6f897dfa4a4ee&dn=ubuntu-16.04.4-desktop-amd64.iso&tr=http://torrent.ubuntu.com:6969/announce&tr=http://ipv6.torrent.ubuntu.com:6969/announce",
                                    ["peers"] = "0",
                                    ["seeds"] = "0",
                                    ["verified"] = "0",
                                }
                            },
                            new RssArticle
                            {
                                Date = new DateTimeOffset(2018, 11, 29, 11, 52, 49, TimeSpan.Zero),
                                Id = "http://releases.ubuntu.com/17.10/ubuntu-17.10.1-desktop-amd64.iso.torrent",
                                Link = new Uri("https://www.ubuntu.com/download/desktop"),
                                Title = "Ubuntu 17.10.1",
                                TorrentUri = new Uri("http://releases.ubuntu.com/17.10/ubuntu-17.10.1-desktop-amd64.iso.torrent"),
                                IsRead = false,
                                AdditionalData = new Dictionary<string, JToken>
                                {
                                    ["fileName"] = "ubuntu-17.10.1-desktop-amd64.iso",
                                    ["infoHash"] = "F07E0B0584745B7BCB35E98097488D34E68623D0",
                                    ["magnetURI"] = "magnet:?xt=urn:btih:f07e0b0584745b7bcb35e98097488d34e68623d0&dn=ubuntu-17.10.1-desktop-amd64.iso&tr=http://torrent.ubuntu.com:6969/announce&tr=http://ipv6.torrent.ubuntu.com:6969/announce9",
                                    ["peers"] = "0",
                                    ["seeds"] = "0",
                                    ["verified"] = "0",
                                }
                            }
                        }
                    }, 
                    new RssFolder("Test folder",
                        new RssItem[]
                        {
                            new RssFeed
                            {
                                Name = "Rutracker",
                                Title = "Rutracker feed",
                                Uid = Guid.Parse("{d4c0a303-7aeb-4199-997c-1ca420501b56}"),
                                Url = new Uri("http://example.com/rutracker.rss"),
                                LastBuildDate = null,
                                IsLoading = false,
                                HasError = false,
                                Articles = new List<RssArticle>
                                {
                                    new RssArticle
                                    {
                                        Date = new DateTimeOffset(2018, 11, 18, 19, 56, 41, TimeSpan.Zero),
                                        Id = "428325a2b3b846759bf7b1089b42bfa2",
                                        Title = "Ubuntu 14.04",
                                        TorrentUri = new Uri("magnet:?xt=urn:btih:9fadb1dcf775fe9a88a7ff0e8150b979d3803398&dn=ubuntu-pack-14.04-unity&tr=http://bt.t-ru.org/ann&tr=http://retracker.local/announce"),
                                        IsRead = false,
                                        Description = "Ubuntu 14.04 i386 and AMD64",
                                        AdditionalData = new Dictionary<string, JToken>
                                        {
                                            ["info_hash"] = "8B0D024760C412F5E0DCBCFAB59B267CBDD895C6",
                                            ["raw_title"] = "ubuntu-pack-14.04-unity"
                                        }
                                    }
                                }
                            }
                        })
                }
            );

            JsonConvert.SerializeObject(rss).ShouldBe(JsonConvert.SerializeObject(expected));
        }

        private JObject ReadJsonFromResource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!)
            using (StreamReader textReader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(textReader))
            {
                return JObject.Load(jsonReader);
            }
        }
    }
}
