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
    /// Statistic
    /// </summary>
    public class Statistic
    {
        /// <summary>
        /// Active torrent count
        /// </summary>
        [JsonPropertyName("activeTorrentCount")]
        public long? ActiveTorrentCount { get; set; }

        /// <summary>
        /// Download speed
        /// </summary>
        [JsonPropertyName("downloadSpeed")]
        public long? downloadSpeed{ get; set; }

        /// <summary>
        /// Paused torrent count
        /// </summary>
        [JsonPropertyName("pausedTorrentCount")]
        public long? pausedTorrentCount{ get; set; }

        /// <summary>
        /// Torrent count
        /// </summary>
        [JsonPropertyName("torrentCount")]
        public long? torrentCount{ get; set; }

        /// <summary>
        /// Upload speed
        /// </summary>
        [JsonPropertyName("uploadSpeed")]
        public long? uploadSpeed{ get; set; }
   
        /// <summary>
        /// Cumulative stats
        /// </summary>
        [JsonPropertyName("cumulative-stats")]
        public CommonStatistic? CumulativeStats { get; set; }
 
        /// <summary>
        /// Current stats
        /// </summary>
        [JsonPropertyName("current-stats")]
        public CommonStatistic? CurrentStats { get; set; }
    }
}
