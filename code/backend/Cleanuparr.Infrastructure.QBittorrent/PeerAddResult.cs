using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents the result of <see cref="IQBittorrentClient2.AddTorrentPeersAsync(IEnumerable{string}, IEnumerable{string}, System.Threading.CancellationToken)"/> operation.
    /// </summary>
    [DebuggerDisplay("Added: {Added}, Failed: {Failed}")]
    public class PeerAddResult
    {
        /// <summary>
        /// The number of added peers.
        /// </summary>
        [JsonProperty("added")]
        public int Added { get; set; }

        /// <summary>
        /// The number of peers that cannot be added.
        /// </summary>
        [JsonProperty("failed")]
        public int Failed { get; set; }
    }
}
