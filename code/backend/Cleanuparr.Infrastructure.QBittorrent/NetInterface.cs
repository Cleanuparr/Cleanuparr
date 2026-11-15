using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represent information about a network interface.
    /// </summary>
    public class NetInterface
    {
        /// <summary>
        /// Gets or sets the internal network interface identifier.
        /// </summary>
        [JsonProperty("value")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the network interface name.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }
    }
}
