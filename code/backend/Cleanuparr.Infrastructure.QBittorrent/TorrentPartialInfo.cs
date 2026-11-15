using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents the changes in information about torrent since the last refresh.
    /// </summary>
    /// <seealso cref="PartialData"/>
    /// <seealso cref="QBittorrentClient.GetPartialDataAsync"/>
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class TorrentPartialInfo
    {
        /// <summary>
        /// Torrent name
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Magnet URI for the torrent
        /// </summary>
        [JsonProperty("magnet_uri")]
        public string MagnetUri { get; set; }

        /// <summary>
        /// Total size (bytes) of files selected for download
        /// </summary>
        [JsonProperty("size")]
        public long? Size { get; set; }

        /// <summary>
        /// Torrent progress (percentage/100)
        /// </summary>
        [JsonProperty("progress")]
        public double? Progress { get; set; }

        /// <summary>
        /// Torrent download speed (bytes/s)
        /// </summary>
        [JsonProperty("dlspeed")]
        public long? DownloadSpeed { get; set; }

        /// <summary>
        /// Torrent upload speed (bytes/s)
        /// </summary>
        [JsonProperty("upspeed")]
        public long? UploadSpeed { get; set; }

        /// <summary>
        /// Torrent priority. Returns -1 if queuing is disabled or torrent is in seed mode
        /// </summary>
        [JsonProperty("priority")]
        public int? Priority { get; set; }

        /// <summary>
        /// Number of seeds connected to
        /// </summary>
        [JsonProperty("num_seeds")]
        public int? ConnectedSeeds { get; set; }

        /// <summary>
        /// Number of seeds in the swarm
        /// </summary>
        [JsonProperty("num_complete")]
        public int? TotalSeeds { get; set; }

        /// <summary>
        /// Number of leechers connected to
        /// </summary>
        [JsonProperty("num_leechs")]
        public int? ConnectedLeechers { get; set; }

        /// <summary>
        /// Number of leechers in the swarm
        /// </summary>
        [JsonProperty("num_incomplete")]
        public int? TotalLeechers { get; set; }

        /// <summary>
        /// Torrent share ratio. Max ratio value is <c>9999</c>. If actual ratio is greater than <c>9999</c>, <c>-1</c> is returned.
        /// </summary>
        [JsonProperty("ratio")]
        public double? Ratio { get; set; }

        /// <summary>
        /// Torrent ETA
        /// </summary>
        [JsonProperty("eta")]
        [JsonConverter(typeof(SecondsToTimeSpanConverter))]
        public TimeSpan? EstimatedTime { get; set; }

        /// <summary>
        /// Torrent state
        /// </summary>
        [JsonProperty("state")]
        [JsonConverter(typeof(TorrentStateConverter))]
        public TorrentState? State { get; set; }

        /// <summary>
        /// True if sequential download is enabled
        /// </summary>
        [JsonProperty("seq_dl")]
        public bool? SequentialDownload { get; set; }

        /// <summary>
        /// True if first last piece are prioritized
        /// </summary>
        [JsonProperty("f_l_piece_prio")]
        public bool? FirstLastPiecePrioritized { get; set; }

        /// <summary>
        /// Category of the torrent
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; }

        /// <summary>
        /// The torrent tags
        /// </summary>
        [JsonProperty("tags")]
        [JsonConverter(typeof(StringToListConverter), ",", true)]
        public IReadOnlyCollection<string> Tags { get; set; }

        /// <summary>
        /// True if super seeding is enabled
        /// </summary>
        [JsonProperty("super_seeding")]
        public bool? SuperSeeding { get; set; }

        /// <summary>
        /// True if force start is enabled for this torrent
        /// </summary>
        [JsonProperty("force_start")]
        public bool? ForceStart { get; set; }

        /// <summary>
        /// The torrent save path
        /// </summary>
        [JsonProperty("save_path")]
        public string SavePath { get; set; }

        /// <summary>
        /// The date and time when the torrent was added
        /// </summary>
        [JsonProperty("added_on")]
        [JsonConverter(typeof(UnixTimeToNullableDateTimeConverter))]
        public DateTime? AddedOn { get; set; }

        /// <summary>
        /// The date and time when the torrent was completed
        /// </summary>
        [JsonProperty("completion_on")]
        [JsonConverter(typeof(UnixTimeToNullableDateTimeConverter))]
        public DateTime? CompletionOn { get; set; }

        /// <summary>
        /// The current torrent tracker
        /// </summary>
        [JsonProperty("tracker")]
        public string CurrentTracker { get; set; }

        /// <summary>
        /// The download speed limit (bytes/second)
        /// </summary>
        [JsonProperty("dl_limit")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public int? DownloadLimit { get; set; }

        /// <summary>
        /// The upload speed limit (bytes/second)
        /// </summary>
        [JsonProperty("up_limit")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public int? UploadLimit { get; set; }

        /// <summary>
        /// The downloaded data size (bytes)
        /// </summary>
        [JsonProperty("downloaded")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? Downloaded { get; set; }

        /// <summary>
        /// The uploaded data size (bytes)
        /// </summary>
        [JsonProperty("uploaded")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? Uploaded { get; set; }

        /// <summary>
        /// The downloaded in this session data size (bytes)
        /// </summary>
        [JsonProperty("downloaded_session")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? DownloadedInSession { get; set; }

        /// <summary>
        /// The uploaded in this session data size (bytes)
        /// </summary>
        [JsonProperty("uploaded_session")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? UploadedInSession { get; set; }

        /// <summary>
        /// The remaining amount to download (bytes)
        /// </summary>
        [JsonProperty("amount_left")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? IncompletedSize { get; set; }

        /// <summary>
        /// The completed amount (bytes)
        /// </summary>
        [JsonProperty("completed")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? CompletedSize { get; set; }

        /// <summary>
        /// The maximal allowed ratio
        /// </summary>
        [JsonProperty("ratio_limit")]
        public double? RatioLimit { get; set; }

        /// <summary>
        /// Upload seeding time limit 
        /// </summary>
        [JsonProperty("seeding_time_limit")]
        [JsonConverter(typeof(MinutesToTimeSpanConverter))]
        public TimeSpan? SeedingTimeLimit { get; set; }

        /// <summary>
        /// Upload inactive seeding time limit 
        /// </summary>
        [JsonProperty("inactive_seeding_time_limit")]
        [JsonConverter(typeof(MinutesToTimeSpanConverter))]
        public TimeSpan? InactiveSeedingTimeLimit { get; set; }

        /// <summary>
        /// The date and time when this torrent was seen complete for the last time
        /// </summary>
        [JsonProperty("seen_complete")]
        [JsonConverter(typeof(UnixTimeToNullableDateTimeConverter))]
        public DateTime? LastSeenComplete { get; set; }

        /// <summary>
        /// The date and time when this torrent was seen active for the last time
        /// </summary>
        [JsonProperty("last_activity")]
        [JsonConverter(typeof(UnixTimeToNullableDateTimeConverter))]
        public DateTime? LastActivityTime { get; set; }

        /// <summary>
        /// The active time of this torrent
        /// </summary>
        [JsonProperty("time_active")]
        [JsonConverter(typeof(SecondsToTimeSpanConverter))]
        public TimeSpan? ActiveTime { get; set; }

        /// <summary>
        /// Indicates whether automatic torrent management is enabled.
        /// </summary>
        [JsonProperty("auto_tmm")]
        public bool? AutomaticTorrentManagement { get; set; }

        /// <summary>
        /// Total torrent size (bytes)
        /// </summary>
        [JsonProperty("total_size")]
        [JsonConverter(typeof(NegativeToNullConverter))]
        public long? TotalSize { get; set; }

        /// <summary>
        /// Additional properties not handled by this library.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented,
                new JsonSerializerSettings() {DefaultValueHandling = DefaultValueHandling.Ignore});
        }
    }
}
