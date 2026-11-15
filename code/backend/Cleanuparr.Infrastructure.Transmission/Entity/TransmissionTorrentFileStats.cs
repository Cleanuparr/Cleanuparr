#nullable enable
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Torrent file stats
    /// </summary>
    public class TransmissionTorrentFileStats
    {
        /// <summary>
        /// Bytes completed
        /// </summary>
        [JsonPropertyName("bytesCompleted")]
        public long? BytesCompleted{ get; set; }

        /// <summary>
        /// Wanted
        /// </summary>
        [JsonPropertyName("wanted")]
        public bool? Wanted{ get; set; }

        /// <summary>
        /// Priority
        /// </summary>
        [JsonPropertyName("priority")]
        public long? Priority{ get; set; }
    }
}
