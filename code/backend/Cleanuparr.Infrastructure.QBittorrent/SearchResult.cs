using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents a search result.
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// Name of the file.
        /// </summary>
        [JsonProperty("fileName")]
        public string FileName { get; set; }

        /// <summary>
        /// Size of the file in bytes.
        /// </summary>
        [JsonProperty("fileSize")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? FileSize { get; set; }

        /// <summary>
        /// Torrent download link (usually either .torrent file or magnet link).
        /// </summary>
        [JsonProperty("fileUrl")]
        public Uri FileUrl { get; set; }

        /// <summary>
        /// Number of leechers.
        /// </summary>
        [JsonProperty("nbLeechers")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? Leechers { get; set; }

        /// <summary>
        /// Number of seeders.
        /// </summary>
        [JsonProperty("nbSeeders")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? Seeds { get; set; }

        /// <summary>
        /// URL of the torrent site
        /// </summary>
        [JsonProperty("siteUrl")]
        public Uri SiteUrl { get; set; }

        /// <summary>
        /// URL of the torrent's description page.
        /// </summary>
        [JsonProperty("descrLink")]
        public Uri DescriptionUrl { get; set; }
    }
}
