using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QBittorrent.Client.Internal
{
    internal static class RssParser
    {
        internal static RssFolder Parse(JObject root)
        {
            var feedSerializer = new JsonSerializer
            {
                DateFormatString = "ddd, dd MMM yyyy HH:mm:ss zzz",
                DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
                Culture = CultureInfo.InvariantCulture,
            };
            return (RssFolder)Convert(root, string.Empty);

            RssItem ConvertFeed(JObject jObject) => jObject.ToObject<RssFeed>(feedSerializer);

            RssItem ConvertFolder(JObject jObject) => new RssFolder(jObject.Properties().Select(p => Convert((JObject) p.Value, p.Name)));

            RssItem Convert(JObject jObject, string name)
            {
                var item = jObject.ContainsKey("uid") ? ConvertFeed(jObject) : ConvertFolder(jObject);
                item.Name = name;
                return item;
            }
        }
    }
}
