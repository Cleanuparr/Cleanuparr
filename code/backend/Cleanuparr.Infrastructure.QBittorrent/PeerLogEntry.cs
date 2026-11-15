using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents QBittorrent peer log entry.
    /// </summary>
    public class PeerLogEntry
    {
        /// <summary>
        /// Gets or sets the ID of the peer.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the IP address of the peer.
        /// </summary>
        [JsonProperty("ip")]
        [JsonConverter(typeof(StringToIpAddressConverter))]
        public IPAddress Address { get; set; }

        /// <summary>
        /// Gets or sets the timestamp (milliseconds since epoch).
        /// </summary>
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the peer was blocked.
        /// </summary>
        [JsonProperty("blocked")]
        public bool Blocked { get; set; }

        /// <summary>
        /// Gets or sets the reason of the block.
        /// </summary>
        [JsonProperty("reason")]
        public string Reason { get; set; }
    }
}
