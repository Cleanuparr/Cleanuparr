using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents global transfer info.
    /// </summary>
    public class GlobalTransferInfo
    {
        /// <summary>
        /// Global download rate (bytes/s)
        /// </summary>
        [JsonProperty("dl_info_speed")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? DownloadSpeed { get; set; }

        /// <summary>
        /// Data downloaded this session (bytes)
        /// </summary>
        [JsonProperty("dl_info_data")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? DownloadedData { get; set; }

        /// <summary>
        /// Download rate limit (bytes/s)
        /// </summary>
        [JsonProperty("dl_rate_limit")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? DownloadSpeedLimit { get; set; }

        /// <summary>
        /// Global upload rate (bytes/s)
        /// </summary>
        [JsonProperty("up_info_speed")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? UploadSpeed { get; set; }

        /// <summary>
        /// Data uploaded this session (bytes)
        /// </summary>
        [JsonProperty("up_info_data")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? UploadedData { get; set; }

        /// <summary>
        /// Upload rate limit (bytes/s)
        /// </summary>
        [JsonProperty("up_rate_limit")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? UploadSpeedLimit { get; set; }

        /// <summary>
        /// DHT nodes connected to
        /// </summary>
        [JsonProperty("dht_nodes")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? DhtNodes { get; set; }

        /// <summary>
        /// Connection status
        /// </summary>
        [JsonProperty("connection_status")]
        public ConnectionStatus? ConnectionStatus { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }
    }
}
