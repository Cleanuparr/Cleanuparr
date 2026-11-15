using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents search status.
    /// </summary>
    public class SearchStatus
    {
        /// <summary>
        /// ID of the search job.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Current status of the search job.
        /// </summary>
        [JsonProperty("status")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SearchJobStatus Status { get; set; }

        /// <summary>
        /// Total number of results.
        /// If the status is <see cref="SearchJobStatus.Running"/> this number may continue to increase.
        /// </summary>
        [JsonProperty("total")]
        public int Total { get; set; }
    }
}
