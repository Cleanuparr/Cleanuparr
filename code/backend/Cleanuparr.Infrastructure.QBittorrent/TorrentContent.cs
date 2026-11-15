using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents torrent content file info.
    /// </summary>
    public class TorrentContent
    {
        /// <summary>
        /// File name (including relative path)
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Size
        /// </summary>
        [JsonProperty("size")]
        public long Size { get; set; }

        /// <summary>
        /// File progress (percentage/100)
        /// </summary>
        [JsonProperty("progress")]
        public double Progress { get; set; }

        /// <summary>
        /// File priority
        /// </summary>
        [JsonProperty("priority")]
        public TorrentContentPriority Priority { get; set; }

        /// <summary>
        /// True if file is seeding/complete
        /// </summary>
        [JsonProperty("is_seed")]
        public bool IsSeeding { get; set; }

        /// <summary>
        /// The range of the file.
        /// </summary>
        [JsonProperty("piece_range")]
        [JsonConverter(typeof(ArrayToRangeConverter))]
        public Range PieceRange { get; set; }

        /// <summary>
        /// Gets the file availability, calculated as <c>(available_pieces)/(total_pieces)</c>
        /// </summary>
        [JsonProperty("availability")]
        public double? Availability { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        /// <summary>
        /// File index
        /// </summary>
        [JsonProperty("index")]
        public int? Index { get; set; }
    }
}
