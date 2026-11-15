using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Torrent peers from
    /// </summary>
    public class TransmissionTorrentPeersFrom
    {
        /// <summary>
        /// From cache
        /// </summary>
        [JsonPropertyName("fromCache")]
        public long? FromCache { get; set; }

        /// <summary>
        /// From DHT
        /// </summary>
        [JsonPropertyName("fromDht")]
        public long? FromDht { get; set; }

        /// <summary>
        /// From incoming
        /// </summary>
        [JsonPropertyName("fromIncoming")]
        public long? FromIncoming{ get; set; }

        /// <summary>
        /// From LPD
        /// </summary>
        [JsonPropertyName("fromLpd")]
        public long? FromLpd{ get; set; }

        /// <summary>
        /// From LTEP
        /// </summary>
        [JsonPropertyName("fromLtep")]
        public long? FromLtep{ get; set; }

        /// <summary>
        /// From PEX
        /// </summary>
        [JsonPropertyName("fromPex")]
        public long? FromPex{ get; set; }

        /// <summary>
        /// From tracker
        /// </summary>
        [JsonPropertyName("fromTracker")]
        public long? FromTracker{ get; set; }
    }
}
