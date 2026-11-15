using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents current search results of a search job.
    /// </summary>
    public class SearchResults
    {
        /// <summary>
        /// Search results.
        /// </summary>
        [JsonProperty("results")]
        public IReadOnlyList<SearchResult> Results { get; set; }
        
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
