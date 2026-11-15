using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transmission.API.RPC.Entity
{
    /// <summary>
    /// Torrent trackers
    /// </summary>
    public class TransmissionTorrentTrackers
    {
        /// <summary>
        /// Announce
        /// </summary>
        [JsonPropertyName("announce")]
        public string? Announce{ get; set; }

        /// <summary>
        /// Id
        /// </summary>
        [JsonPropertyName("id")]
        public long Id{ get; set; }

        /// <summary>
        /// Scrape
        /// </summary>
        [JsonPropertyName("scrape")]
        public string? Scrape{ get; set; }

        /// <summary>
        /// Site name
        /// </summary>
        [JsonPropertyName("sitename")]
        public string? SiteName { get; set; }

        /// <summary>
        /// Tier
        /// </summary>
        [JsonPropertyName("tier")]
        public long? Tier{ get; set; }
    }
}
