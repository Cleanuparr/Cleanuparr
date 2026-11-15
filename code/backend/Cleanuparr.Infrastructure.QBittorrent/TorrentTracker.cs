using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents the torrent tracker information.
    /// </summary>
    public class TorrentTracker
    {
        private static readonly IDictionary<string, TorrentTrackerStatus> StatusMap = 
            new Dictionary<string, TorrentTrackerStatus>(StringComparer.OrdinalIgnoreCase)
            {
                ["Disabled"] = TorrentTrackerStatus.Disabled,
                ["Working"] = TorrentTrackerStatus.Working,
                ["Updating..."] = TorrentTrackerStatus.Updating,
                ["Not working"] = TorrentTrackerStatus.NotWorking,
                ["Not contacted yet"] = TorrentTrackerStatus.NotContacted
            };

        /// <summary>
        /// Tracker URL
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; }

        /// <summary>
        /// Tracker status as string
        /// </summary>
        /// <seealso cref="TrackerStatus"/>
        /// <remarks>
        /// <para>Until API v2.2.0 this property might return localized status string.</para>
        /// <para>Starting from API v2.2.0 this property always returns English status string.</para>
        /// </remarks>
        [JsonIgnore]
        public string Status { get; set; }

        /// <summary>
        /// Tracker status
        /// </summary>
        /// <seealso cref="Status"/>
        /// <remarks>
        /// Until API v2.2.0 this property might always return null on non-English localizations.
        /// </remarks>
        [JsonIgnore]
        public TorrentTrackerStatus? TrackerStatus { get; set; }

        /// <summary>
        /// Number of peers for current torrent reported by the tracker
        /// </summary>
        [JsonProperty("num_peers")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public int? Peers { get; set; }

        /// <summary>
        /// Number of seeds for current torrent reported by the tracker
        /// </summary>
        [JsonProperty("num_seeds")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public int? Seeds { get; set; }

        /// <summary>
        /// Number of leeches for current torrent reported by the tracker
        /// </summary>
        [JsonProperty("num_leeches")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public int? Leeches { get; set; }

        /// <summary>
        /// Number of completed downloads for current torrent reported by the tracker
        /// </summary>
        [JsonProperty("num_downloaded")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public int? CompletedDownloads { get; set; }

        /// <summary>
        /// Tracker priority tier. Lower tier trackers are tried before higher tiers
        /// </summary>
        [JsonProperty("tier")]
        [ApiLevel(ApiLevel.V2, MinVersion = "2.2.0")]
        public int? Tier { get; set; }

        /// <summary>
        /// Tracker message (there is no way of knowing what this message is - it's up to tracker admins)
        /// </summary>
        [JsonProperty("msg")]
        public string Message { get; set; }


        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        [OnDeserialized]
        [UsedImplicitly]
        private void OnDeserialized(StreamingContext context)
        {
            const string statusKey = "status";
            if (AdditionalData != null &&
                AdditionalData.TryGetValue(statusKey, out JToken status))
            {
                if (status.Type == JTokenType.String)
                {
                    Status = status.ToObject<string>();
                    TrackerStatus = StringToStatus(Status);
                    AdditionalData.Remove(statusKey);
                }
                else if (status.Type == JTokenType.Integer)
                {
                    TrackerStatus = status.ToObject<TorrentTrackerStatus>();
                    Status = StatusToString(TrackerStatus);
                    AdditionalData.Remove(statusKey);
                }
            }
        }
        
        private static string StatusToString(TorrentTrackerStatus? status)
        {
            return StatusMap.FirstOrDefault(x => x.Value == status).Key;
        }

        private static TorrentTrackerStatus? StringToStatus(string statusString)
        {
            return StatusMap.TryGetValue(statusString, out var status) ? status : (TorrentTrackerStatus?)null;
        }
    }
}
