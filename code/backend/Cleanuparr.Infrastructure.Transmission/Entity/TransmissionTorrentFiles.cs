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
    /// Torrent files
    /// </summary>
    public class TransmissionTorrentFiles
    {
        /// <summary>
        /// Bytes completed
        /// </summary>
        [JsonPropertyName("bytesCompleted")]
        public long? BytesCompleted{ get; set; }

        /// <summary>
        /// Length
        /// </summary>
        [JsonPropertyName("length")]
        public long? Length{ get; set; }

        /// <summary>
        /// Name
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name{ get; set; }

        /// <summary>
        /// First piece index of file
        /// </summary>
        [JsonPropertyName("beginPiece")]
        public long? BeginPiece { get; set; }

        /// <summary>
        /// Last piece index of file
        /// </summary>
        [JsonPropertyName("endPiece")]
        public long? EndPiece { get; set; }
    }
}
