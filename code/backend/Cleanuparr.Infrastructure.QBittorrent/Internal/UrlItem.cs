using Newtonsoft.Json;
using System;

namespace QBittorrent.Client.Internal
{
    internal struct UrlItem
    {
        [JsonProperty("url")]
        public Uri Url { get; set; }
    }
}
