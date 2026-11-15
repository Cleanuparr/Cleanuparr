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
    /// Torrent information
    /// </summary>
    public class TorrentInfo
    {
        /// <summary>
        /// The torrent's unique Id.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Activity date
        /// </summary>
        [JsonPropertyName("activityDate")]
        public long? ActivityDate { get; set; }

        /// <summary>
        /// Added date
        /// </summary>
        [JsonPropertyName("addedDate")]
        public long? AddedDate { get; set; }

        /// <summary>
        /// Availability
        /// </summary>
        [JsonPropertyName("availability")]
        [JsonConverter(typeof(IntOrArrayConverter))] // Without this converter, Transmission < 4.0.0 leads to an error.
        public long[]? Availability { get; set; }

        /// <summary>
        /// Torrents bandwidth priority
        /// </summary>
        [JsonPropertyName("bandwidthPriority")]
        public long? BandwidthPriority { get; set; }

        /// <summary>
        /// Comment
        /// </summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        /// <summary>
        /// Corrupt ever
        /// </summary>
        [JsonPropertyName("corruptEver")]
        public long? CorruptEver { get; set; }

        /// <summary>
        /// Creator
        /// </summary>
        [JsonPropertyName("creator")]
        public string? Creator { get; set; }

        /// <summary>
        /// Date created
        /// </summary>
        [JsonPropertyName("dateCreated")]
        public long? DateCreated { get; set; }

        /// <summary>
        /// Desired available
        /// </summary>
        [JsonPropertyName("desiredAvailable")]
        public long? DesiredAvailable { get; set; }

        /// <summary>
        /// Done date
        /// </summary>
        [JsonPropertyName("doneDate")]
        public long? DoneDate { get; set; }

        /// <summary>
        /// Download directory
        /// </summary>
        [JsonPropertyName("downloadDir")]
        public string? DownloadDir { get; set; }

        /// <summary>
        /// Downloaded ever
        /// </summary>
        [JsonPropertyName("downloadedEver")]
        public long? DownloadedEver { get; set; }

        /// <summary>
        /// Download limit
        /// </summary>
        [JsonPropertyName("downloadLimit")]
        public long? DownloadLimit { get; set; }

        /// <summary>
        /// Download limited
        /// </summary>
        [JsonPropertyName("downloadLimited")]
        public bool? DownloadLimited { get; set; }

        /// <summary>
        /// Edit date
        /// </summary>
        [JsonPropertyName("editDate")]
        public long? EditDate { get; set; }

        /// <summary>
        /// Error
        /// </summary>
        [JsonPropertyName("error")]
        public long? Error { get; set; }

        /// <summary>
        /// Error string
        /// </summary>
        [JsonPropertyName("errorString")]
        public string? ErrorString { get; set; }

        /// <summary>
        /// ETA
        /// </summary>
        [JsonPropertyName("eta")]
        public long? Eta { get; set; }

        /// <summary>
        /// ETA idle
        /// </summary>
        [JsonPropertyName("etaIdle")]
        public long? EtaIdle { get; set; }

        /// <summary>
        /// File count
        /// </summary>
        [JsonPropertyName("file-count")]
        public long? FileCount { get; set; }

        /// <summary>
        /// Files
        /// </summary>
        [JsonPropertyName("files")]
        public TransmissionTorrentFiles[]? Files { get; set; }

        /// <summary>
        /// File stats
        /// </summary>
        [JsonPropertyName("fileStats")]
        public TransmissionTorrentFileStats[]? FileStats { get; set; }

        /// <summary>
        /// Group
        /// </summary>
        [JsonPropertyName("group")]
        public string? Group { get; set; }

        /// <summary>
        /// Hash string
        /// </summary>
        [JsonPropertyName("hashString")]
        public string? HashString { get; set; }

        /// <summary>
        /// Have unchecked
        /// </summary>
        [JsonPropertyName("haveUnchecked")]
        public long? HaveUnchecked { get; set; }

        /// <summary>
        /// Have valid
        /// </summary>
        [JsonPropertyName("haveValid")]
        public long? HaveValid { get; set; }

        /// <summary>
        /// Honors session limits
        /// </summary>
        [JsonPropertyName("honorsSessionLimits")]
        public bool? HonorsSessionLimits { get; set; }

        /// <summary>
        /// Is finished
        /// </summary>
        [JsonPropertyName("isFinished")]
        public bool? IsFinished { get; set; }

        /// <summary>
        /// Is private
        /// </summary>
        [JsonPropertyName("isPrivate")]
        public bool? IsPrivate { get; set; }

        /// <summary>
        /// Is stalled
        /// </summary>
        [JsonPropertyName("isStalled")]
        public bool? IsStalled { get; set; }

        /// <summary>
        /// Labels
        /// </summary>
        [JsonPropertyName("labels")]
        public string[]? Labels { get; set; }

        /// <summary>
        /// Left until done
        /// </summary>
        [JsonPropertyName("leftUntilDone")]
        public long? LeftUntilDone { get; set; }

        /// <summary>
        /// Magnet link
        /// </summary>
        [JsonPropertyName("magnetLink")]
        public string? MagnetLink { get; set; }

        /// <summary>
        /// Manual announce time
        /// </summary>
        [JsonPropertyName("manualAnnounceTime")]
        public long? ManualAnnounceTime { get; set; }

        /// <summary>
        /// Max connected peers
        /// </summary>
        [JsonPropertyName("maxConnectedPeers")]
        public long? MaxConnectedPeers { get; set; }

        /// <summary>
        /// Metadata percent complete
        /// </summary>
        [JsonPropertyName("metadataPercentComplete")]
        public double? MetadataPercentComplete { get; set; }

        /// <summary>
        /// Name
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Peer limit
        /// </summary>
        [JsonPropertyName("peer-limit")]
        public long? PeerLimit { get; set; }

        /// <summary>
        /// Peers
        /// </summary>
        [JsonPropertyName("peers")]
        public TransmissionTorrentPeers[]? Peers { get; set; }

        /// <summary>
        /// Peers connected
        /// </summary>
        [JsonPropertyName("peersConnected")]
        public long? PeersConnected { get; set; }

        /// <summary>
        /// Peers from
        /// </summary>
        [JsonPropertyName("peersFrom")]
        public TransmissionTorrentPeersFrom? PeersFrom { get; set; }

        /// <summary>
        /// Peers getting from us
        /// </summary>
        [JsonPropertyName("peersGettingFromUs")]
        public long? PeersGettingFromUs { get; set; }

        /// <summary>
        /// Peers sending to us
        /// </summary>
        [JsonPropertyName("peersSendingToUs")]
        public long? PeersSendingToUs { get; set; }

        /// <summary>
        /// Percent complete
        /// </summary>
        [JsonPropertyName("percentComplete")]
        public double? PercentComplete { get; set; }

        /// <summary>
        /// Percent done
        /// </summary>
        [JsonPropertyName("percentDone")]
        public double? PercentDone { get; set; }

        /// <summary>
        /// Pieces
        /// </summary>
        [JsonPropertyName("pieces")]
        public string? Pieces { get; set; }

        /// <summary>
        /// Piece count
        /// </summary>
        [JsonPropertyName("pieceCount")]
        public long? PieceCount { get; set; }

        /// <summary>
        /// Piece size
        /// </summary>
        [JsonPropertyName("pieceSize")]
        public long? PieceSize { get; set; }

        /// <summary>
        /// Priorities
        /// </summary>
        [JsonPropertyName("priorities")]
        public long[]? Priorities { get; set; }

        /// <summary>
        /// Primary mime type
        /// </summary>
        [JsonPropertyName("primary-mime-type")]
        public string? PrimaryMimeType { get; set; }

        /// <summary>
        /// Queue position
        /// </summary>
        [JsonPropertyName("queuePosition")]
        public long? QueuePosition { get; set; }

        /// <summary>
        /// Rate download
        /// </summary>
        [JsonPropertyName("rateDownload")]
        public long? RateDownload { get; set; }

        /// <summary>
        /// Rate upload
        /// </summary>
        [JsonPropertyName("rateUpload")]
        public long? RateUpload { get; set; }

        /// <summary>
        /// Recheck progress
        /// </summary>
        [JsonPropertyName("recheckProgress")]
        public double? RecheckProgress { get; set; }

        /// <summary>
        /// Seconds downloading
        /// </summary>
        [JsonPropertyName("secondsDownloading")]
        public long? SecondsDownloading { get; set; }

        /// <summary>
        /// Seconds seeding
        /// </summary>
        [JsonPropertyName("secondsSeeding")]
        public long? SecondsSeeding { get; set; }

        /// <summary>
        /// Seed idle limit
        /// </summary>
        [JsonPropertyName("seedIdleLimit")]
        public long? SeedIdleLimit { get; set; }

        /// <summary>
        /// Seed idle mode
        /// </summary>
        [JsonPropertyName("seedIdleMode")]
        public long? SeedIdleMode { get; set; }

        /// <summary>
        /// Seed ratio limit
        /// </summary>
        [JsonPropertyName("seedRatioLimit")]
        public double? SeedRatioLimit { get; set; }

        /// <summary>
        /// Seed ratio mode
        /// </summary>
        [JsonPropertyName("seedRatioMode")]
        public long? SeedRatioMode { get; set; }

        /// <summary>
        /// Sequential Download
        /// </summary>
        [JsonPropertyName("sequentialDownload")]
        public bool? SequentialDownload { get; set; }

        /// <summary>
        /// Size when done
        /// </summary>
        [JsonPropertyName("sizeWhenDone")]
        public long? SizeWhenDone { get; set; }

        /// <summary>
        /// Start date
        /// </summary>
        [JsonPropertyName("startDate")]
        public long? StartDate { get; set; }

        /// <summary>
        /// Status
        /// </summary>
        [JsonPropertyName("status")]
        public long? Status { get; set; }

        /// <summary>
        /// Trackers
        /// </summary>
        [JsonPropertyName("trackers")]
        public TransmissionTorrentTrackers[]? Trackers { get; set; }

        /// <summary>
        /// Tracker list:
        /// A string of announce URLs, one per line, with a blank
        /// line between tiers
        /// </summary>
        [JsonPropertyName("trackerList")]
        public string? TrackerList { get; set; }

        /// <summary>
        /// Tracker stats
        /// </summary>
        [JsonPropertyName("trackerStats")]
        public TransmissionTorrentTrackerStats[]? TrackerStats { get; set; }

        /// <summary>
        /// Total size
        /// </summary>
        [JsonPropertyName("totalSize")]
        public long? TotalSize { get; set; }

        /// <summary>
        /// Torrent file
        /// </summary>
        [JsonPropertyName("torrentFile")]
        public string? TorrentFile { get; set; }

        /// <summary>
        /// Uploaded ever
        /// </summary>
        [JsonPropertyName("uploadedEver")]
        public long? UploadedEver { get; set; }

        /// <summary>
        /// Upload limit
        /// </summary>
        [JsonPropertyName("uploadLimit")]
        public long? UploadLimit { get; set; }

        /// <summary>
        /// Upload limited
        /// </summary>
        [JsonPropertyName("uploadLimited")]
        public bool? UploadLimited { get; set; }

        /// <summary>
        /// Upload ratio
        /// </summary>
        [JsonPropertyName("uploadRatio")]
        public double? uploadRatio { get; set; }

        /// <summary>
        /// Wanted
        /// </summary>
        [JsonPropertyName("wanted")]
        public bool[]? Wanted { get; set; }

        /// <summary>
        /// Web seeds
        /// </summary>
        [JsonPropertyName("webseeds")]
        public string[]? Webseeds { get; set; }

        /// <summary>
        /// Web seeds sending to us
        /// </summary>
        [JsonPropertyName("webseedsSendingToUs")]
        public long? WebseedsSendingToUs { get; set; }
    }
}
