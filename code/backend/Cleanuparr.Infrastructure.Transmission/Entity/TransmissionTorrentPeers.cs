using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Torrent peers
    /// </summary>
    public class TransmissionTorrentPeers
    {
        /// <summary>
        /// Address
        /// </summary>
        [JsonPropertyName("address")]
        public string? Address{ get; set; }

        /// <summary>
        /// Client name
        /// </summary>
        [JsonPropertyName("clientName")]
        public string? ClientName{ get; set; }

        /// <summary>
        /// Client is choked
        /// </summary>
        [JsonPropertyName("clientIsChoked")]
        public bool? ClientIsChoked{ get; set; }

        /// <summary>
        /// Client is Interested
        /// </summary>
        [JsonPropertyName("clientIsInterested")]
        public bool? ClientIsInterested{ get; set; }

        /// <summary>
        /// Flag string
        /// </summary>
        [JsonPropertyName("flagStr")]
        public string? FlagStr{ get; set; }

        /// <summary>
        /// Is downloading from
        /// </summary>
        [JsonPropertyName("isDownloadingFrom")]
        public bool? IsDownloadingFrom{ get; set; }

        /// <summary>
        /// Is encrypted
        /// </summary>
        [JsonPropertyName("isEncrypted")]
        public bool? IsEncrypted { get; set; }

        /// <summary>
        /// Is incoming
        /// </summary>
        [JsonPropertyName("isIncoming")]
        public bool? IsIncoming { get; set; }

        /// <summary>
        /// Is uploading to
        /// </summary>
        [JsonPropertyName("isUploadingTo")]
        public bool? IsUploadingTo{ get; set; }

        /// <summary>
        /// Is UTP
        /// </summary>
        [JsonPropertyName("isUTP")]
        public bool? IsUtp{ get; set; }

        /// <summary>
        /// Peer is choked
        /// </summary>
        [JsonPropertyName("peerIsChoked")]
        public bool? PeerIsChoked{ get; set; }

        /// <summary>
        /// Peer is Interested
        /// </summary>
        [JsonPropertyName("peerIsInterested")]
        public bool? PeerIsInterested{ get; set; }

        /// <summary>
        /// Port
        /// </summary>
        [JsonPropertyName("port")]
        public long? Port{ get; set; }

        /// <summary>
        /// Progress
        /// </summary>
        [JsonPropertyName("progress")]
        public double? Progress{ get; set; }

        /// <summary>
        /// Rate to client
        /// </summary>
        [JsonPropertyName("rateToClient")]
        public long? RateToClient{ get; set; }

        /// <summary>
        /// Rate to peer
        /// </summary>
        [JsonPropertyName("rateToPeer")]
        public long? RateToPeer{ get; set; }
    }
}
