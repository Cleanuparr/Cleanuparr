using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents full or partial data returned by <see cref="QBittorrentClient.GetPeerPartialDataAsync"/> method.
    /// </summary>
    public class PeerPartialData
    {
        /// <summary>
        /// Gets or sets the response identifier.
        /// </summary>
        /// <value>
        /// The response identifier.
        /// </value>
        [JsonProperty("rid")]
        public int ResponseId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this object contains all data.
        /// </summary>
        /// <value>
        ///   <see langword="true" /> if this object contains all data; 
        ///   <see langword="false" /> if this object contains only changes of data since the previous request.
        /// </value>
        [JsonProperty("full_update")]
        public bool FullUpdate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether flags must be shown.
        /// </summary>
        /// <value>
        ///   <see langword="true" /> if flags must be shown; otherwise, <see langword="false" />.
        /// </value>
        [JsonProperty("show_flags")]
        public bool ShowFlags { get; set; }

        /// <summary>
        /// Gets or sets the changed and new peers.
        /// </summary>
        [JsonProperty("peers")]
        public IReadOnlyDictionary<string, PeerPartialInfo> PeersChanged { get; set; }

        /// <summary>
        /// Gets or sets the removed peers.
        /// </summary>
        [JsonProperty("peers_removed")]
        public IReadOnlyList<string> PeersRemoved { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }
    }
}
