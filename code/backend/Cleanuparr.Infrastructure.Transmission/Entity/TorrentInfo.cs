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

    /// <summary>
    /// Torrent files
    /// </summary>
    public class TransmissionTorrentFiles
    {
        /// <summary>
        /// Bytes completed
        /// </summary>
        [JsonPropertyName("bytesCompleted")]
        public long? BytesCompleted{ get; set; }

        /// <summary>
        /// Length
        /// </summary>
        [JsonPropertyName("length")]
        public long? Length{ get; set; }

        /// <summary>
        /// Name
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name{ get; set; }

        /// <summary>
        /// First piece index of file
        /// </summary>
        [JsonPropertyName("beginPiece")]
        public long? BeginPiece { get; set; }

        /// <summary>
        /// Last piece index of file
        /// </summary>
        [JsonPropertyName("endPiece")]
        public long? EndPiece { get; set; }
    }

    /// <summary>
    /// Torrent file stats
    /// </summary>
    public class TransmissionTorrentFileStats
    {
        /// <summary>
        /// Bytes completed
        /// </summary>
        [JsonPropertyName("bytesCompleted")]
        public long? BytesCompleted{ get; set; }

        /// <summary>
        /// Wanted
        /// </summary>
        [JsonPropertyName("wanted")]
        public bool? Wanted{ get; set; }

        /// <summary>
        /// Priority
        /// </summary>
        [JsonPropertyName("priority")]
        public long? Priority{ get; set; }
    }

    /// <summary>
    /// Torrent peers
    /// </summary>
    public class TransmissionTorrentPeers
    {
        /// <summary>
        /// Address
        /// </summary>
        [JsonPropertyName("address")]
        public string? Address{ get; set; }

        /// <summary>
        /// Client name
        /// </summary>
        [JsonPropertyName("clientName")]
        public string? ClientName{ get; set; }

        /// <summary>
        /// Client is choked
        /// </summary>
        [JsonPropertyName("clientIsChoked")]
        public bool? ClientIsChoked{ get; set; }

        /// <summary>
        /// Client is Interested
        /// </summary>
        [JsonPropertyName("clientIsInterested")]
        public bool? ClientIsInterested{ get; set; }

        /// <summary>
        /// Flag string
        /// </summary>
        [JsonPropertyName("flagStr")]
        public string? FlagStr{ get; set; }

        /// <summary>
        /// Is downloading from
        /// </summary>
        [JsonPropertyName("isDownloadingFrom")]
        public bool? IsDownloadingFrom{ get; set; }

        /// <summary>
        /// Is encrypted
        /// </summary>
        [JsonPropertyName("isEncrypted")]
        public bool? IsEncrypted { get; set; }

        /// <summary>
        /// Is incoming
        /// </summary>
        [JsonPropertyName("isIncoming")]
        public bool? IsIncoming { get; set; }

        /// <summary>
        /// Is uploading to
        /// </summary>
        [JsonPropertyName("isUploadingTo")]
        public bool? IsUploadingTo{ get; set; }

        /// <summary>
        /// Is UTP
        /// </summary>
        [JsonPropertyName("isUTP")]
        public bool? IsUtp{ get; set; }

        /// <summary>
        /// Peer is choked
        /// </summary>
        [JsonPropertyName("peerIsChoked")]
        public bool? PeerIsChoked{ get; set; }

        /// <summary>
        /// Peer is Interested
        /// </summary>
        [JsonPropertyName("peerIsInterested")]
        public bool? PeerIsInterested{ get; set; }

        /// <summary>
        /// Port
        /// </summary>
        [JsonPropertyName("port")]
        public long? Port{ get; set; }

        /// <summary>
        /// Progress
        /// </summary>
        [JsonPropertyName("progress")]
        public double? Progress{ get; set; }

        /// <summary>
        /// Rate to client
        /// </summary>
        [JsonPropertyName("rateToClient")]
        public long? RateToClient{ get; set; }

        /// <summary>
        /// Rate to peer
        /// </summary>
        [JsonPropertyName("rateToPeer")]
        public long? RateToPeer{ get; set; }
    }

    /// <summary>
    /// Torrent peers from
    /// </summary>
    public class TransmissionTorrentPeersFrom
    {
        /// <summary>
        /// From cache
        /// </summary>
        [JsonPropertyName("fromCache")]
        public long? FromCache { get; set; }

        /// <summary>
        /// From DHT
        /// </summary>
        [JsonPropertyName("fromDht")]
        public long? FromDht { get; set; }

        /// <summary>
        /// From incoming
        /// </summary>
        [JsonPropertyName("fromIncoming")]
        public long? FromIncoming{ get; set; }

        /// <summary>
        /// From LPD
        /// </summary>
        [JsonPropertyName("fromLpd")]
        public long? FromLpd{ get; set; }

        /// <summary>
        /// From LTEP
        /// </summary>
        [JsonPropertyName("fromLtep")]
        public long? FromLtep{ get; set; }

        /// <summary>
        /// From PEX
        /// </summary>
        [JsonPropertyName("fromPex")]
        public long? FromPex{ get; set; }

        /// <summary>
        /// From tracker
        /// </summary>
        [JsonPropertyName("fromTracker")]
        public long? FromTracker{ get; set; }
    }

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

    /// <summary>
    /// Torrent tracker stats
    /// </summary>
    public class TransmissionTorrentTrackerStats
    {
        /// <summary>
        /// Announce
        /// </summary>
        [JsonPropertyName("announce")]
        public string? Announce{ get; set; }

        /// <summary>
        /// Announce state
        /// </summary>
        [JsonPropertyName("announceState")]
        public long? AnnounceState{ get; set; }

        /// <summary>
        /// Download count
        /// </summary>
        [JsonPropertyName("downloadCount")]
        public long? DownloadCount{ get; set; }

        /// <summary>
        /// Has announced
        /// </summary>
        [JsonPropertyName("hasAnnounced")]
        public bool? HasAnnounced{ get; set; }

        /// <summary>
        /// Has scraped
        /// </summary>
        [JsonPropertyName("hasScraped")]
        public bool? HasScraped{ get; set; }

        /// <summary>
        /// Host
        /// </summary>
        [JsonPropertyName("host")]
        public string? Host{ get; set; }

        /// <summary>
        /// Is backup
        /// </summary>
        [JsonPropertyName("isBackup")]
        public bool? IsBackup{ get; set; }

        /// <summary>
        /// Last announce peer count
        /// </summary>
        [JsonPropertyName("lastAnnouncePeerCount")]
        public long? LastAnnouncePeerCount{ get; set; }

        /// <summary>
        /// Id
        /// </summary>
        [JsonPropertyName("id")]
        public long? Id{ get; set; }

        /// <summary>
        /// Last announce result 
        /// </summary>
        [JsonPropertyName("lastAnnounceResult")]
        public string? LastAnnounceResult{ get; set; }

        /// <summary>
        /// Last announce succeeded
        /// </summary>
        [JsonPropertyName("lastAnnounceSucceeded")]
        public bool? LastAnnounceSucceeded{ get; set; }

        /// <summary>
        /// Last announce start time
        /// </summary>
        [JsonPropertyName("lastAnnounceStartTime")]
        public long? LastAnnounceStartTime{ get; set; }

        /// <summary>
        /// Last scrape result
        /// </summary>
        [JsonPropertyName("lastScrapeResult")]
        public string? LastScrapeResult{ get; set; }

        /// <summary>
        /// Last announce timed out
        /// </summary>
        [JsonPropertyName("lastAnnounceTimedOut")]
        public bool? LastAnnounceTimedOut{ get; set; }

        /// <summary>
        /// Last announce time
        /// </summary>
        [JsonPropertyName("lastAnnounceTime")]
        public long? LastAnnounceTime{ get; set; }

        /// <summary>
        /// Last scrape scceeded
        /// </summary>
        [JsonPropertyName("lastScrapeSucceeded")]
        public bool? LastScrapeSucceeded{ get; set; }

        /// <summary>
        /// Last scrape start time
        /// </summary>
        [JsonPropertyName("lastScrapeStartTime")]
        public long? LastScrapeStartTime{ get; set; }

        /// <summary>
        /// Last scrape timed out
        /// </summary>
        [JsonPropertyName("lastScrapeTimedOut")]
        public bool? LastScrapeTimedOut{ get; set; }

        /// <summary>
        /// Last scrape time
        /// </summary>
        [JsonPropertyName("lastScrapeTime")]
        public long? LastScrapeTime{ get; set; }

        /// <summary>
        /// Scrape
        /// </summary>
        [JsonPropertyName("scrape")]
        public string? Scrape{ get; set; }

        /// <summary>
        /// Tier
        /// </summary>
        [JsonPropertyName("tier")]
        public long? Tier{ get; set; }

        /// <summary>
        /// Leecher count
        /// </summary>
        [JsonPropertyName("leecherCount")]
        public long? LeecherCount{ get; set; }

        /// <summary>
        /// Next announce time
        /// </summary>
        [JsonPropertyName("nextAnnounceTime")]
        public long? NextAnnounceTime{ get; set; }

        /// <summary>
        /// Next scrape time
        /// </summary>
        [JsonPropertyName("nextScrapeTime")]
        public long? NextScrapeTime{ get; set; }

        /// <summary>
        /// Scrape state
        /// </summary>
        [JsonPropertyName("scrapeState")]
        public long? ScrapeState{ get; set; }

        /// <summary>
        /// Seeder count
        /// </summary>
        [JsonPropertyName("seederCount")]
        public long? SeederCount{ get; set; }

        /// <summary>
        /// Site name
        /// </summary>
        [JsonPropertyName("sitename")]
        public string? SiteName { get; set; }
    }

    /// <summary>
    /// Contains arrays of torrents, and removed torrents in an integer array
    /// </summary>
    public class TransmissionTorrents
    {
        /// <summary>
        /// Array of torrents
        /// </summary>
        [JsonPropertyName("torrents")]
        public TorrentInfo[]? Torrents{ get; set; }

        /// <summary>
        /// Array of torrent-id numbers of recently-removed torrents
        /// </summary>
        [JsonPropertyName("removed")]
        public long[]? Removed{ get; set; }
    }
}
