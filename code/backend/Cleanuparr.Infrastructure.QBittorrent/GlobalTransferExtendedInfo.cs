using Newtonsoft.Json;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents global transfer info with additional information and statistics.
    /// </summary>
    public class GlobalTransferExtendedInfo : GlobalTransferInfo
    {
        /// <summary>
        /// The amount of data downloaded for all time (bytes)
        /// </summary>
        [JsonProperty("alltime_dl")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? AllTimeDownloaded { get; set; }

        /// <summary>
        /// The amount of data uploaded for all time (bytes)
        /// </summary>
        [JsonProperty("alltime_ul")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? AllTimeUploaded { get; set; }

        /// <summary>
        /// The ammount of data wasted (bytes)
        /// </summary>
        [JsonProperty("total_wasted_session")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? TotalWasted { get; set; }

        /// <summary>
        /// Total number of peer connections
        /// </summary>
        [JsonProperty("total_peer_connections")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? TotalPeerConnections { get; set; }
        
        /// <summary>
        /// Total used buffers size
        /// </summary>
        [JsonProperty("total_buffers_size")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? TotalBuffersSize { get; set; }

        /// <summary>
        /// Indicates whether the global alternative speed limits are enabled
        /// </summary>
        [JsonProperty("use_alt_speed_limits")]
        public bool GlobalAltSpeedLimitsEnabled { get; set; }

        /// <summary>
        /// The preferred refresh interval (milliseconds)
        /// </summary>
        [JsonProperty("refresh_interval")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public int? RefreshInterval { get; set; }

        /// <summary>
        /// Free space on disk.
        /// </summary>
        /// <remarks>This value is available starting from API v2.1.1.</remarks>
        [JsonProperty("free_space_on_disk")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.1.1")]
        public long? FreeSpaceOnDisk { get; set; }

        /// <summary>
        /// Global ratio
        /// </summary>
        [JsonIgnore]
        public double? GlobalRatio => AllTimeUploaded / (double?) AllTimeDownloaded;
    }
}
